using System.Collections.Generic;
using System.Threading;
using CollectionManager.DataTypes;
using CollectionManager.Enums;
using CollectionManager.Modules.CollectionsManager;
using CollectionManager.Modules.FileIO.OsuDb;
using CollectionManagerExtensionsDll.DataTypes;
using CollectionManagerExtensionsDll.Modules.API.osu;
using CollectionManager.Modules.ModParser;
using CollectionManager.Modules.FileIO;
using CollectionManager.Modules.FileIO.OsuScoresDb;
using System.Linq;
using System;

namespace CollectionManagerExtensionsDll.Modules.CollectionGenerator
{
    public class UserLocalScoresGenerator
    {
        private readonly string StartingProcessing = "Preparing...";
        private readonly string ParsingUser = "Processing \"{0}\" | {1}";
        private readonly string GettingScores = "Getting scores from api...(try {0} of 5)";
        private readonly string GettingBeatmaps = "Getting missing beatmaps data from api... {0}";
        private readonly string ParsingFinished = "Done processing {0} users! - Close this window to add created collections";
        private readonly string GettingUserFailed = "FAILED | Waiting {1}s and trying again.";
        private readonly string GettingBeatmapFailed = "FAILED | Waiting {1}s and trying again.";
        private readonly string Aborted = "FAILED | User aborted.";
        private int _currentUserMissingMapCount;

        private readonly CollectionsManager _collectionManager;
        private readonly OsuFileIo _osuFileIo;
        private LogCollectionGeneration _logger;
        readonly Dictionary<string, IList<Score>> _scoreCache = new Dictionary<string, IList<Score>>();
        private readonly Dictionary<string, Scores> scoreListClone;
        private readonly List<Score> highscores = new List<Score>();
        private static ModParser modParser = new ModParser();

        public UserLocalScoresGenerator(OsuFileIo osuFileIo, string OsuDirectory)
        {
            _osuFileIo = osuFileIo;
            if (((ScoresCacher)_osuFileIo.ScoresDatabase).ScoreList.Count == 0)
            {
                _osuFileIo.ScoresLoader.ReadDb(OsuDirectory + "\\scores.db");
            }

            scoreListClone = ((ScoresCacher)_osuFileIo.ScoresDatabase).ScoreList;

            //highest score first
            foreach (KeyValuePair<string, Scores> pair in scoreListClone)
            {
                if (pair.Value.Count > 0)
                {
                    foreach(var group in pair.Value.GroupBy(s => s.Mods))
                    {
                        highscores.Add(group.Aggregate((s1, s2) => (s1.TotalScore > s2.TotalScore) ? s1 : s2));
                    }
                }
            }
            _collectionManager = new CollectionsManager(_osuFileIo.LoadedMaps.Beatmaps);
        }

        public Collections GetPlayersCollections(CollectionGeneratorConfiguration cfg, LogCollectionGeneration logger)
        {
            int totalUsernames = cfg.Usernames.Count;
            int processedCounter = 0;
            var c = new Collections();

            _logger = logger;
            _logger?.Invoke(StartingProcessing, 0d);
            _collectionManager.EditCollection(CollectionEditArgs.ClearCollections());
            try
            {
                foreach (var username in cfg.Usernames)
                {
                    var collections = GetPlayerCollections(username,
                        cfg.CollectionNameSavePattern, cfg.ScoreSaveConditions);
                    Log(username, ParsingFinished,
                        ++processedCounter / (double)totalUsernames * 100);
                    _collectionManager.EditCollection(CollectionEditArgs.AddOrMergeCollections(collections));
                }

                c.AddRange(_collectionManager.LoadedCollections);
                _logger?.Invoke(string.Format(ParsingFinished, cfg.Usernames.Count), 100);

                _logger = null;
                return c;
            }
            catch (ThreadAbortException)
            {
                _logger?.Invoke(Aborted, -1d);
                return c;
            }
        }

        private Collections GetPlayerCollections(string username, string collectionNameSavePattern,
            ScoreSaveConditions configuration)
        {
            _currentUserMissingMapCount = 0;
            var validScores = GetPlayerScores(username, configuration);
            Dictionary<string, Beatmaps> collectionsDict = new Dictionary<string, Beatmaps>();
            var collections = new Collections();
            foreach (var s in validScores)
            {
                if (configuration.IsEgibleForSaving(s))
                {
                    string collectionName = CreateCollectionName(s, username, collectionNameSavePattern);
                    if (collectionsDict.ContainsKey(collectionName))
                        collectionsDict[collectionName].Add(_osuFileIo.LoadedMaps.GetByHash(s.MapHash));
                    else
                        collectionsDict.Add(collectionName, new Beatmaps() { _osuFileIo.LoadedMaps.GetByHash(s.MapHash) });
                }
            }
            foreach (var c in collectionsDict)
            {
                var collection = new Collection(_osuFileIo.LoadedMaps) { Name = c.Key };
                foreach (var beatmap in c.Value)
                {
                    collection.AddBeatmap(beatmap);
                }
                collections.Add(collection);
            }
            return collections;
        }

        private string _lastUsername = "";
        private void Log(string username, string message, double precentage = -1d)
        {
            if (string.IsNullOrEmpty(username))
                username = _lastUsername;
            else
                _lastUsername = username;
            _logger?.Invoke(string.Format(ParsingUser, username, message), precentage);
        }

        private IList<Score> GetPlayerScores(string username, ScoreSaveConditions configuration)
        {
            Log(username, string.Format(GettingScores, 1));
            if (_scoreCache.ContainsKey(username))
                return _scoreCache[username];

            List<Score> egibleScores = new List<Score>();
            IList<Score> scores;

            do
            {
                int i = 1;
                int Cooldown = 20;
                do
                {
                    Log(username, string.Format(GettingScores, i));
                    scores = highscores.Where(s => s.PlayerName == username).ToList();
                } while (scores == null && i++ < 5);
                if (scores == null)
                {
                    Log(username, string.Format(GettingUserFailed, i, Cooldown));
                    Thread.Sleep(Cooldown * 1000);
                }
            } while (scores == null);

            _scoreCache.Add(username, scores);
            foreach (var s in scores)
            {
                if (configuration.IsEgibleForSaving(s))
                    egibleScores.Add(s);
            }
            return egibleScores;
        }

        public static string CreateCollectionName(Score score, string username, string collectionNameFormat)
        {
            try
            {
                //return String.Format(collectionNameFormat, username,
                //  modParser.GetModsFromEnum(score.EnabledMods, true));
                return String.Format(collectionNameFormat, username,
                  modParser.GetModsFromEnum(score.Mods, true));
            }
            catch (FormatException ex)
            {
                return "Invalid format!";
            }
        }

        public delegate void LogCollectionGeneration(string logMessage, double precentage);
    }
}
