﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using App.Interfaces;
using App.Misc;
using App.Models;
using App.Presenters.Controls;
using App.Presenters.Forms;
using CollectionManager.DataTypes;
using CollectionManager.Modules.CollectionsManager;
using CollectionManager.Modules.FileIO;
using CollectionManagerExtensionsDll.DataTypes;
using CollectionManagerExtensionsDll.Modules.API.osu;
using CollectionManagerExtensionsDll.Modules.CollectionGenerator;
using GuiComponents.Interfaces;
using NAudio.Codecs;

namespace App
{
    public class SidePanelActionsHandler
    {
        private readonly OsuFileIo _osuFileIo;
        private readonly ICollectionEditor _collectionEditor;
        private readonly IUserDialogs _userDialogs;
        private readonly IMainFormView _mainForm;

        IBeatmapListingForm _beatmapListingForm;
        private IUserTopGeneratorForm _userTopGeneratorForm;
        private IUsernameGeneratorForm _usernameGeneratorForm;
        private IDownloadManagerFormView _downloadManagerForm;
        private readonly IBeatmapListingBindingProvider _beatmapListingBindingProvider;
        private readonly MainFormPresenter _mainFormPresenter;
        private readonly ILoginFormView _loginForm;
        private CollectionsGenerator _collectionGenerator;
        private OsuSite _osuSite = new OsuSite();
        public SidePanelActionsHandler(OsuFileIo osuFileIo, ICollectionEditor collectionEditor, IUserDialogs userDialogs, IMainFormView mainForm, IBeatmapListingBindingProvider beatmapListingBindingProvider, MainFormPresenter mainFormPresenter, ILoginFormView loginForm)
        {
            _osuFileIo = osuFileIo;
            _collectionEditor = collectionEditor;
            _userDialogs = userDialogs;
            _mainForm = mainForm;
            _beatmapListingBindingProvider = beatmapListingBindingProvider;
            _mainFormPresenter = mainFormPresenter;
            _loginForm = loginForm;
            _collectionGenerator = new CollectionsGenerator(Initalizer.OsuFileIo.LoadedMaps);

            BindMainFormActions();
        }

        private void BindMainFormActions()
        {
            _mainForm.SidePanelView.LoadCollection += (s, a) => LoadCollectionFile();
            _mainForm.SidePanelView.LoadDefaultCollection += (s, a) => LoadDefaultCollection();
            _mainForm.SidePanelView.ClearCollections += (s, a) => ClearCollections();
            _mainForm.SidePanelView.SaveCollections += (s, a) => SaveCollections();
            _mainForm.SidePanelView.SaveInvidualCollections += (s, a) => SaveInvidualCollections();

            _mainForm.SidePanelView.ShowBeatmapListing += (s, a) => ShowBeatmapListing();
            _mainForm.SidePanelView.ShowDownloadManager += (s, a) => ShowDownloadManager();
            _mainForm.SidePanelView.DownloadAllMissing += (s, a) => DownloadAllMissing();
            _mainForm.SidePanelView.GenerateCollections += (s, a) => GenerateCollections();

            _mainFormPresenter.InfoTextModel.UpdateTextClicked += FormUpdateTextClicked;
            _mainForm.Closing += FormOnClosing;
        }

        private void GenerateCollections()
        {
            if (_userTopGeneratorForm == null || _userTopGeneratorForm.IsDisposed)
            {
                _userTopGeneratorForm = GuiComponentsProvider.Instance.GetClassImplementing<IUserTopGeneratorForm>();
                var model = new UserTopGeneratorModel((a) => 
                    _collectionGenerator.CreateCollectionName(new ApiScore() {EnabledMods = (int) (Mods.Hr | Mods.Hd)},
                    "Piotrekol", a));
                model.GenerateUsernames += GenerateUsernames;
                new UserTopGeneratorFormPresenter(model, _userTopGeneratorForm);
                model.Start += (s, a) => _collectionGenerator.GenerateCollection(model.GeneratorConfiguration);
                model.SaveCollections +=
                    (s, a) => _collectionEditor.EditCollection(CollectionEditArgs.AddCollections(model.Collections));
                model.Abort += (s, a) => _collectionGenerator.Abort();
                _collectionGenerator.StatusUpdated +=
                    (s, a) =>
                    {
                        model.GenerationStatus = _collectionGenerator.Status;
                        model.GenerationCompletionPrecentage = _collectionGenerator.ProcessingCompletionPrecentage;
                    };

                _collectionGenerator.CollectionsUpdated +=
                    (s, a) => model.Collections = _collectionGenerator.Collections;
            }
            _userTopGeneratorForm.Show();
        }


        private void GenerateUsernames(object sender, EventArgs eventArgs)
        {
            if (_usernameGeneratorForm == null || _usernameGeneratorForm.IsDisposed)
            {
                _usernameGeneratorForm = GuiComponentsProvider.Instance.GetClassImplementing<IUsernameGeneratorForm>();
                var model = new UsernameGeneratorModel();
                model.Start +=
                    (s, a) =>
                    {
                        new Thread(() =>
                        {
                            model.GeneratedUsernames = _osuSite.GetUsernames(model.StartRank, model.EndRank,
                           (string logMessage, int completionPrecentage) =>
                           {
                               model.Status = logMessage;
                               model.CompletionPrecentage = completionPrecentage;
                           });
                            model.EmitComplete();
                        }).Start();
                    };
                model.Complete += (s, a) => Helpers.SetClipboardText(model.GeneratedUsernamesStr);
                new UsernameGeneratorPresenter(model, _usernameGeneratorForm.view);
            }
            _usernameGeneratorForm.ShowAndBlock();
        }

        private void DownloadAllMissing()
        {
            var downloadableBeatmaps = new Beatmaps();
            foreach (var collection in Initalizer.LoadedCollections)
            {
                foreach (var beatmap in collection.DownloadableBeatmaps)
                {
                    downloadableBeatmaps.Add(beatmap);
                }
            }

            if (downloadableBeatmaps.Count > 0)
                if (OsuDownloadManager.Instance.AskUserForSaveDirectoryAndLogin(_userDialogs, _loginForm))
                {
                    OsuDownloadManager.Instance.DownloadBeatmaps(downloadableBeatmaps);
                    ShowDownloadManager();
                }
        }

        private void FormUpdateTextClicked(object sender, EventArgs args)
        {
            var updater = _mainFormPresenter.InfoTextModel.GetUpdater();
            if (updater.IsUpdateAvaliable())
            {
                if (!string.IsNullOrWhiteSpace(updater.newVersionLink))
                    Process.Start(updater.newVersionLink);
            }

        }

        private void FormOnClosing(object sender, EventArgs eventArgs)
        {
            if (_beatmapListingForm != null && !_beatmapListingForm.IsDisposed)
            {
                _beatmapListingForm.Close();
            }
            if (_downloadManagerForm != null && !_downloadManagerForm.IsDisposed)
            {
                _downloadManagerForm.Close();
            }
        }
        private void LoadCollectionFile()
        {
            var fileLocation = _userDialogs.SelectFile("", "Collection database (*.db/*.osdb)|*.db;*.osdb",
                    "collection.db");
            if (fileLocation == string.Empty) return;
            var loadedCollections = _osuFileIo.CollectionLoader.LoadCollection(fileLocation);
            _collectionEditor.EditCollection(CollectionEditArgs.AddCollections(loadedCollections));
        }

        private void LoadDefaultCollection()
        {
            var fileLocation = Path.Combine(Initalizer.OsuDirectory, "collection.db");
            if (File.Exists(fileLocation))
            {
                var loadedCollections = _osuFileIo.CollectionLoader.LoadCollection(fileLocation);
                _collectionEditor.EditCollection(CollectionEditArgs.AddCollections(loadedCollections));
            }
        }

        private void ClearCollections()
        {
            _collectionEditor.EditCollection(CollectionEditArgs.ClearCollections());
        }

        private void SaveCollections()
        {
            var fileLocation = _userDialogs.SaveFile("Where collection file should be saved?", "osu! Collection database (.db)|*.db|CM database (.osdb)|*.osdb");
            if (fileLocation == string.Empty) return;
            _osuFileIo.CollectionLoader.SaveCollection(Initalizer.LoadedCollections, fileLocation);
        }

        private void SaveInvidualCollections()
        {
            var saveDirectory = _userDialogs.SelectDirectory("Where collection files should be saved?", true);
            if (saveDirectory == string.Empty) return;
            foreach (var collection in Initalizer.LoadedCollections)
            {
                var filename = Helpers.StripInvalidCharacters(collection.Name);
                _osuFileIo.CollectionLoader.SaveCollection(new Collections() { collection }, saveDirectory + filename);
            }
        }

        private void ShowBeatmapListing()
        {
            if (_beatmapListingForm == null || _beatmapListingForm.IsDisposed)
            {
                _beatmapListingForm = GuiComponentsProvider.Instance.GetClassImplementing<IBeatmapListingForm>();
                var presenter = new BeatmapListingFormPresenter(_beatmapListingForm);
                _beatmapListingBindingProvider.Bind(presenter.BeatmapListingModel);
                _beatmapListingForm.Closing += (s, a) => _beatmapListingBindingProvider.UnBind(presenter.BeatmapListingModel);
            }
            _beatmapListingForm.Show();
        }

        private void CreateDownloadManagerForm()
        {
            if (_downloadManagerForm == null || _downloadManagerForm.IsDisposed)
            {
                _downloadManagerForm = GuiComponentsProvider.Instance.GetClassImplementing<IDownloadManagerFormView>();
                new DownloadManagerFormPresenter(_downloadManagerForm, new DownloadManagerModel(OsuDownloadManager.Instance));
            }
        }
        private void ShowDownloadManager()
        {
            CreateDownloadManagerForm();
            _downloadManagerForm.Show();
        }
    }
}