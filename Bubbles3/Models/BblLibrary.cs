using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bubbles3.Models
{
    
    public enum BookOperations { Add, Replace, Remove };
    public struct BookOperationData
    {
        public BblBook b;
        public BookOperations operation;
        public BblBook replace;
        public BookOperationData(BblBook b, BookOperations operation, BblBook replace)
        {
            this.b = b;
            this.operation = operation;
            this.replace = replace;
        }
    }
    public class BblLibrary:IDisposable
    {
        public static TaskScheduler mainThread;

        public event EventHandler LibraryLoaded;
        private BblLibraryRootNode _root;
        public BblLibraryRootNode Root => _root;

        private ObservableCollection<BblBook> _books = new ObservableCollection<BblBook>();

        Task _populatingTask;
        CancellationTokenSource _populatingCancelTokenSrc = new CancellationTokenSource();
        
        Progress<BookOperationData> _progress;

        private FileSystemWatcher _fileWatcher = new FileSystemWatcher();
        private FileSystemWatcher _dirWatcher = new FileSystemWatcher();

        public bool IsDisposed { get; private set; }

        public BblLibrary(DirectoryInfoEx root)
        {
            if(mainThread == null) mainThread = TaskScheduler.FromCurrentSynchronizationContext();

            _root = new BblLibraryRootNode(root);

            _progress = new Progress<BookOperationData>((bookOperationData) => { OnBookOperation(bookOperationData); });

            _populatingTask = Task.Run(() => BuildLibrary(_progress), _populatingCancelTokenSrc.Token);
            _populatingTask.ContinueWith((t1) => { OnPopulatingCompleted(); },
                _populatingCancelTokenSrc.Token, TaskContinuationOptions.OnlyOnRanToCompletion, mainThread);

            _fileWatcher.Path = root.FullName;
            _dirWatcher.Path = root.FullName;

            _fileWatcher.IncludeSubdirectories = true;
            _dirWatcher.IncludeSubdirectories = true;

            _fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            _dirWatcher.NotifyFilter = NotifyFilters.DirectoryName;

            _fileWatcher.Created += OnFileCreated;
            _fileWatcher.Deleted += OnFileDeleted;
            _fileWatcher.Renamed += OnFileRenamed;
            _dirWatcher.Created += OnDirectoryCreated;
            _dirWatcher.Deleted += OnDirectoryDeleted;
            _dirWatcher.Renamed += OnDirectoryRenamed;

            // Begin watching.
            _fileWatcher.EnableRaisingEvents = true;
            _dirWatcher.EnableRaisingEvents = true;
        }

        public ObservableCollection<BblBook> Books
        {
            get { return _books; }
            private set { }
        }

        public void OnBookOperation(BookOperationData data)
        {
            BblBook b = data.b;
            BookOperations operation = data.operation;
            BblBook replace = data.replace;

            if (operation == BookOperations.Add)
            { 
                lock (b.NodeLock) _books.Add(b);
            }
            else if (operation == BookOperations.Replace)
            {
                lock (b.NodeLock) _books[_books.IndexOf(replace)] = b;
            }
            else if (operation == BookOperations.Remove)
            {
                lock (b.NodeLock) _books.Remove(b);
            }
        }


        void BuildLibrary(IProgress<BookOperationData> progress)
        {
            _populatingCancelTokenSrc.Token.ThrowIfCancellationRequested();
            try { 
            _root.OnBookOperation = progress.Report;
            _root.Inflate();
            }
            catch(OperationCanceledException e) { Console.WriteLine(e.Message + " : Library Inflation Interrupted"); }
        }

        public void OnPopulatingCompleted()
        {
            _populatingTask = null;
            //_root.OnBookOperation = OnBookOperation;
            if (IsDisposed) return;
            LibraryLoaded(this, new EventArgs());
        }

        public async Task UntilFileExists(string path)
        {
            int timeout = 300;
            DateTime start = DateTime.Now;
            while(!File.Exists(path))
            {
                await Task.Delay(50);
                if ((DateTime.Now - start).TotalSeconds > timeout) break;
            }
        }

        private void OnDirectoryCreated(object sender, FileSystemEventArgs e)
        {
            Task.Run(()=> {

                _root.AddDirectory(e.FullPath);
            });
        }

        private void OnDirectoryDeleted(object sender, FileSystemEventArgs e)
        {
            Task.Run(()=> {

                _root.DeleteDirectory(e.FullPath);
            });
            
        }

        private void OnDirectoryRenamed(object sender, RenamedEventArgs e)
        {
            Task.Run(()=>
            {
                _root.RenameDirectory(e.FullPath, e.OldFullPath);
            });
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            string path = e.FullPath;
            Task.Run(()=> { OnFileCreatedAsync(path); });
        }
        async void OnFileCreatedAsync(string path)
        {
            if (Path.GetExtension(path) == ".crdownload") return;
            await UntilFileExists(path);
            _root.AddFile(path);
        }
        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            Task.Run(()=>_root.DeleteFile(e.FullPath));
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            Task.Run(()=>OnFileRenamedAsync(e.FullPath, e.OldFullPath));
        }
        async void OnFileRenamedAsync(string newPath, string oldPath)
        {
            await UntilFileExists(newPath);
            _root.RenameFile(newPath, oldPath);
        }
        public void CloseLibrary(bool saveToDb)
        {
            Dispose();
        }

        /// <summary>
        /// Cleanup
        /// </summary>
        public async void Dispose()
        {
            IsDisposed = true;

            _fileWatcher.Created -= OnFileCreated;
            _fileWatcher.Deleted -= OnFileDeleted;
            _fileWatcher.Renamed -= OnFileRenamed;
            _dirWatcher.Created -= OnDirectoryCreated;
            _dirWatcher.Deleted -= OnDirectoryDeleted;
            _dirWatcher.Renamed -= OnDirectoryRenamed;

            if (_populatingTask != null)
            {
                _populatingCancelTokenSrc.Cancel();
                while (!(_populatingTask.IsCanceled || _populatingTask.IsCompleted || _populatingTask.IsFaulted))
                    await Task.Delay(100);
            }

            _populatingCancelTokenSrc.Dispose();

            _dirWatcher.EnableRaisingEvents = false;
            _fileWatcher.EnableRaisingEvents = false;

            _dirWatcher.Dispose();
            _fileWatcher.Dispose();
        }

        //public void OnPopulatingCancelled()
        //{
        //    _populatingTask = null;
            
        //}
    }
}
