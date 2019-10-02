using Bubbles3.Utils;
using Bubbles3.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Bubbles3.Models
{

    public abstract class BblBook : BblLibraryNode
    {

        public event EventHandler Renamed;
        public event EventHandler Populated;
        public event EventHandler Unpopulated;
        public event EventHandler ThumbnailLoaded;
        public event EventHandler Demoted;
        public event EventHandler Deleted;
        public override bool IsBook => true;
        public enum BookType { Directory, Archive, Pdf };
        public bool IsPopulated => _pages != null && _pages.Count > 0;
        public bool IsPopulating => _cancelPopulateTask != null;

        protected Task _populateTask;

        protected CancellationTokenSource _cancelPopulateTask = null;

        public bool IsThumbnailLoaded => _pages!= null && _pages.Count > 0 &&  _pages[0].IsThumbnailLoaded;
        public bool IsThumbnailLoading => _pages != null && _pages.Count>0 && !_pages[0].IsThumbnailLoaded && _pages[0].IsImageLoading;

        protected bool _demoted; //book has no pages or BblBookDirectory renamed with no .book extension
        protected BblBook(BblLibraryRootNode root, BblLibraryNode parent, WIN32_FIND_DATA findData, BookType type) : base(root, parent, findData)
        {
            Initialize(type);
        }
        protected BblBook(BblLibraryRootNode root, BblLibraryNode parent, FileSystemInfoEx info, BookType type) : base(root, parent, info)
        {
            Initialize(type);
        }
        
        /// <summary>
        /// Swap constructor, takes a generic BblLibraryNode (normal directory, not book) that is to be replaced with a BblBook
        /// Happens when the node for an empty directory is promoted to book status because there's now an image in it.
        /// </summary>
        protected BblBook(BblLibraryNode node, BookType type) : base(node) 
        {
            Type = type;
            BblBook b = node as BblBook;
            if (b != null)
            {
                Root.OnBookOperation(new BookOperationData(this, BookOperations.Replace, node as BblBook));
                Index = b.Index;
                _pages = b._pages;
            }
            else
            {
                Root.OnBookOperation(new BookOperationData(this, BookOperations.Add, null));
                Index = Root.BookIndex++;
                Root.BookCount++;
            }
        }
        ~BblBook()
        {
            if(Application.Current?.Dispatcher != null)
            { 
            Application.Current.Dispatcher.BeginInvokeIfRequired(
                    DispatcherPriority.Background,
                    new Action(() =>
                    {
                        lock (_lock)
                        {
                            if (Open) OnClosed();
                            if (IsPopulated) UnPopulateAsync();
                            CleanupEventHandlers();
                        }
                    }));
            }
        }
        void Initialize(BookType type)
        {
            Type = type;
            Root.OnBookOperation(new BookOperationData(this, BookOperations.Add, null));
            Index = Root.BookIndex++;
            Root.BookCount++;
        }
        public BookType Type { get; private set; }


        public int Index { get; set; }


        public virtual int PageCount
        {
            get
            {
                if (_pages == null || _pages.Count == 0) return -1;
                else return _pages.Count;
            }
        }

        protected ObservableCollection<BblPage> _pages;
        public ObservableCollection<BblPage> Pages
        {
            get { return _pages; }
            set { _pages = value; }
        }

        private static BitmapSource _icon = null;
        public BitmapSource Icon
        {
            get
            {
                if (_icon == null) _icon = new BitmapImage(new Uri(@"/icons/BblBookIcon.png", UriKind.Relative));
                return _icon;
            }
        }
        public virtual BitmapSource Thumbnail
        {
            get
            {
                if (_pages != null && _pages.Count > 0 && _pages[0].Thumbnail != null)
                    return _pages[0].Thumbnail;
                return Icon;
            }
        }


        protected virtual string IvpPath => Path + ".ivp";

        protected Dictionary<string, ImageViewingParams> _ivps;


        public async void PopulateAsync(int priority)
        {
            if (IsPopulating) return;
            if (IsPopulated && ! IsPopulating)
            {
                if (IsThumbnailLoaded || IsThumbnailLoading) Populated(this, new EventArgs());
                else LoadThumbnail(priority);
                return;
            }

            _cancelPopulateTask = new CancellationTokenSource();
            try
            {
                IProgress<bool> progress = new Progress<bool>((populated) => 
                {
                    if (populated) Populated(this, new EventArgs());
                    else
                    {
                        Root.OnBookOperation(new BookOperationData(this, BookOperations.Remove, null));
                        _demoted = true;
                        if (Demoted != null) Demoted(this, new EventArgs());
                    }
                });
                
                await(_populateTask = BblTask.Run(() =>
                {
                    try
                    {
                        _cancelPopulateTask.Token.ThrowIfCancellationRequested();
                        Populate();
                        _cancelPopulateTask.Token.ThrowIfCancellationRequested();

                        if (_pages != null && _pages.Count > 0)
                        {
                            DeserializeIVP();
                            progress.Report(true);
                            _pages[0].ThumbnailLoaded += OnThumbnailLoaded;
                            _pages[0].LoadImageAsync(priority);
                        }
                        else
                        {
                            UnPopulate(); 
                            progress.Report(false);
                        }
                        _cancelPopulateTask.Token.ThrowIfCancellationRequested();
                    }
                    catch /*(Exception e)*/{  UnPopulate();  }
                    finally
                    {
                        lock(_lock)
                        {
                            _populateTask = null;
                            if (_cancelPopulateTask != null) _cancelPopulateTask.Dispose();
                            _cancelPopulateTask = null;
                        }
                    }
                }
                , priority, _cancelPopulateTask.Token));
            }
            catch
            {
                UnPopulate();
            }

        }

        protected abstract void Populate();
        public  void LoadThumbnail(int priority)
        {
            
            if(IsThumbnailLoaded )
            {
                if (!IsThumbnailLoading && ThumbnailLoaded != null) ThumbnailLoaded(this, EventArgs.Empty);
                return;
            }

            if (IsPopulated) _pages[0].LoadImageAsync(priority);
        }
        public virtual void ReleaseCoverData()
        {
            if(IsThumbnailLoaded) _pages[0].ReleaseAllData();
        }

        public void OnThumbnailLoaded(object sender, EventArgs args)
        {
            try { 
            ThumbnailLoaded(this, new EventArgs());
            }
            catch
            {

            }
        }

        public async void UnPopulateAsync(EventHandler OnClosed=null)
        {
            if (_cancelPopulateTask != null)
            {
                lock (_lock) { _cancelPopulateTask.Cancel(); }
                await _populateTask;
            }
            UnPopulate();
            if (OnClosed != null) OnClosed(this, EventArgs.Empty);
        }

        protected virtual void UnPopulate()
        {
            Application.Current.Dispatcher.BeginInvokeIfRequired(DispatcherPriority.Normal, new Action(() => { 
                lock (_lock)
                {
                    if (_pages != null)
                    {
                        if(_ivps != null)
                        {
                            SerializeIVP();
                            _ivps.Clear();
                            _ivps = null;
                        }

                        foreach (var p in _pages) p.Close();

                        _pages.Clear();
                        _pages = null;
                    }
                }
                if (Unpopulated != null) Unpopulated(this, EventArgs.Empty);
            }));
        }

        #region IVPS
        public bool Open { get; set; }
        public ImageViewingParams GetIvp(string filename)
        {
            if (_ivps == null) throw new InvalidOperationException();

            ImageViewingParams ivp;
            _ivps.TryGetValue(filename, out ivp);
            return ivp;
        }

        public void SetIvp(string filename, ImageViewingParams value)
        {
            if (_ivps == null ) throw new InvalidOperationException();
            _ivps[value.filename] = value;

        }
        public void OnOpened()
        {
            Open = true;
            if (_ivps == null) throw new InvalidDataException();
        }

        /// <summary>
        /// Restore image viewing parameters from
        /// serialized file.
        /// </summary>
        public virtual void DeserializeIVP()
        {
            if (_ivps != null) throw new InvalidOperationException();

            lock (_lock) {
                _ivps = new Dictionary<string, ImageViewingParams>();
                foreach (var p in _pages)
                {
                    ImageViewingParams ivp = new ImageViewingParams();
                    ivp.filename = p.Filename;
                    if(!_ivps.ContainsKey(p.Filename)) _ivps.Add(p.Filename, ivp);
                }
            }

            if (File.Exists(IvpPath))
            {
                var deserializer = new System.Xml.Serialization.XmlSerializer(typeof(IvpCollection));
                TextReader reader = null;
                IvpCollection XmlData = null;
                object obj = null;

                try
                {
                    reader = new StreamReader(IvpPath);
                    obj = deserializer.Deserialize(reader);
                }
                catch
                {
                    if (reader != null) reader.Close();
                    reader = null;
                    File.Delete(IvpPath);
                }
                finally
                {
                    if (obj != null) XmlData = (IvpCollection)obj;
                    List<ImageViewingParams> ivps = null;
                    if (XmlData != null) ivps = new List<ImageViewingParams>(XmlData.Collection);
                    if (reader != null) reader.Close();

                    if (ivps != null)
                    {
                        lock(_lock)
                        {
                            foreach (var ivp in ivps)
                            {
                                if (ivp.filename == null)
                                    continue;
                                _ivps[ivp.filename] = ivp;
                            }
                        }
                    }
                }
            }
        }
        public void OnClosed()
        {
            if (Open == true) SerializeIVP();
            Open = false;
        }
        
        public void CleanupEventHandlers()
        {
            if (ThumbnailLoaded != null) foreach (Delegate d in ThumbnailLoaded.GetInvocationList()) ThumbnailLoaded -= (EventHandler)d;
            if (Populated != null) foreach (Delegate d in Populated.GetInvocationList()) Populated -= (EventHandler)d;
            if (Unpopulated != null) foreach (Delegate d in Unpopulated.GetInvocationList()) Unpopulated -= (EventHandler)d;
            if (Renamed != null) foreach (Delegate d in Renamed.GetInvocationList()) Renamed -= (EventHandler)d;
            if (Demoted != null) foreach (Delegate d in Demoted.GetInvocationList()) Demoted -= (EventHandler)d;
            if (Deleted != null) foreach (Delegate d in Deleted.GetInvocationList()) Deleted -= (EventHandler)d;
        }


        /// <summary>
        /// Serialize Image viewing parameters
        /// save each image's last view transforms
        /// </summary>
        public virtual void SerializeIVP()
        {
            try
            {
                if (_ivps == null || _pages == null || _pages.Count == 0 || !Directory.Exists(System.IO.Path.GetDirectoryName(IvpPath)))
                    throw new InvalidOperationException();
            }
            catch
            {
                return;
            }
            


            foreach (BblPage p in _pages)
            {
                if (_ivps[p.Filename].isDirty) 
                {
                    var ivp = _ivps[p.Filename];
                    ivp.isDirty = false;
                    _ivps[p.Filename] = ivp;
                }
            }

            List<ImageViewingParams> ivps = _ivps.Values.ToList();
            if (Directory.Exists(Path))
            {
                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(IvpCollection));
                using (TextWriter writer = new StreamWriter(IvpPath))
                {
                    serializer.Serialize(writer, new IvpCollection(ivps));
                }
            }
        }


        #endregion

        public abstract void LoadPageData(int index, CancellationToken cancel);

        public virtual bool CanRenameFile => File.Exists(Path);
        public virtual bool RenameFile(string newName, bool silent=false)
        {
            if(CanRenameFile)
            {
                string newPath = System.IO.Path.GetDirectoryName(Path) + "\\" + newName;
                try
                {
                    File.Move(Path, newPath);
                    return true;
                }
                catch (Exception e)
                {
                    if (!silent) System.Windows.MessageBox.Show(e.Message, "Rename Failure", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation);
                }
            }
            return false;
        }
        public virtual bool CanMoveFile => File.Exists(Path);
        public virtual bool MoveFile(string parentDirectory, bool silent = false)
        {
            if(CanMoveFile)
            {
                string newPath = parentDirectory + "\\" + Name;
                try
                {
                    File.Move(Path, newPath);
                    return true;
                }
                catch (Exception e)
                {
                    if (!silent) System.Windows.MessageBox.Show(e.Message, "Move File Failure", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation);
                }
            }
            return false;
        }

        public virtual bool CanDeleteFile => File.Exists(Path);
        public virtual bool DeleteFile(bool silent = false)
        {
            if (CanDeleteFile)
            {
                try
                {
                    RecycleBin.DeleteFileOrFolder(Path);
                    return true;
                }
                catch (Exception e)
                {
                    if (!silent) System.Windows.MessageBox.Show(e.Message, "Thrash File Failure", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation);
                }
            }
            return false;
        }

        public bool CanDeletePages => Type == BookType.Directory;
        public bool CanRenamePages => Type == BookType.Directory;

        protected void FireThumbnailLoaded()
        {
            if (ThumbnailLoaded != null) ThumbnailLoaded(this, EventArgs.Empty);
        }
        protected void FirePopulated()
        {
            if (Populated != null) Populated(this, EventArgs.Empty);
        }
        protected void FireUnpopulated()
        {
            if (Unpopulated != null) Unpopulated(this, EventArgs.Empty);
        }
        protected void FireRenamed()
        {
            if (Renamed != null) Renamed(this, EventArgs.Empty);
        }
        protected void FireDeleted()
        {
            if (Deleted != null) Deleted(this, EventArgs.Empty);
        }
        protected void FireDemoted()
        {
            if (Demoted != null) Demoted(this, EventArgs.Empty);
        }
    }

}
