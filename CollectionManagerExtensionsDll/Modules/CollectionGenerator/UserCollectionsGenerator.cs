using System;
using System.Threading;
using CollectionManager.DataTypes;
using CollectionManager.Modules.FileIO.OsuDb;
using CollectionManagerExtensionsDll.DataTypes;

namespace CollectionManagerExtensionsDll.Modules.CollectionGenerator
{
    public class UserCollectionsGenerator
    {
        public event EventHandler CollectionsUpdated;
        public event EventHandler StatusUpdated;
        private string _status = "";
        public string Status
        {
            get { return _status; }
            set
            {
                _status = value;
                StatusUpdated?.Invoke(this, EventArgs.Empty);
            }
        }
        public double ProcessingCompletionPrecentage { get; set; }

        private Thread _processingThread;
        private Collections _collections;
        private readonly MapCacher _loadedBeatmaps;
        private UserLocalScoresGenerator _userLocalScoreGenerator;
        private CollectionManager.Modules.FileIO.OsuFileIo _osuFileIo;
        private readonly string _osuDirectory;

        public Collections Collections
        {
            get { return _collections; }
            set
            {
                _collections = value;
                CollectionsUpdated?.Invoke(this, EventArgs.Empty);
            }
        }
        public UserCollectionsGenerator (CollectionManager.Modules.FileIO.OsuFileIo osuFileIo, string osuDirectory)
        {
            _osuFileIo = osuFileIo;
            _osuDirectory = osuDirectory;
        }

        public string CreateCollectionName(Score score, string username, string collectionNameFormat)
        {
            return UserLocalScoresGenerator.CreateCollectionName(score, username, collectionNameFormat);
        }

        public void GenerateCollection(CollectionGeneratorConfiguration configuration)
        {
            if (_userLocalScoreGenerator == null)
                _userLocalScoreGenerator = new UserLocalScoresGenerator(_osuFileIo, _osuDirectory);
            if (_processingThread == null || !_processingThread.IsAlive)
            {
                _processingThread = new Thread(() =>
                {
                    Collections = _userLocalScoreGenerator.GetPlayersCollections(configuration, Log);
                });
                _processingThread.Start();
            }

        }

        public void Log(string message, double precentage)
        {
            ProcessingCompletionPrecentage = precentage;
            Status = message;
        }

        public void Abort()
        {
            if (_processingThread.IsAlive)
            {
                _processingThread.Abort();
                try
                {
                    _processingThread.Join();
                }
                catch { }
                Collections = new Collections();

            }
        }
    }
}
