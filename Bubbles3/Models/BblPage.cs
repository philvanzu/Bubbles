using Bubbles3.Utils;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Bubbles3.Models
{

    public class BblPage : IComparable
    {
        public event EventHandler ThumbnailLoaded;

        CancellationTokenSource _cancelImageLoad;
        Task _dataLoaderTask;
        public bool IsImageLoaded => Image != null;
        public bool IsImageLoading => _cancelImageLoad != null;
        public bool IsThumbnailLoaded => Thumbnail != null;


        public object _lock = new object();
        public BblBook Book { get; set; }

        public int Index => (Book.Pages != null) ? Book.Pages.IndexOf(this) : 0;

        public string Filename { get; set; }
        public string FileExtension => System.IO.Path.GetExtension(Filename);
        public string Path { get; set; }

        public long Size { get; set; }

        public DateTime CreationTime { get; set; }
        public DateTime LastAccessTime { get; set; }
        public DateTime LastWriteTime { get; set; }
        public BblImgSource Image { get; set; }
        public BitmapSource Thumbnail { get; set; }

        public BblPage(BblBook book)
        {
            Book = book;
        }
        ~BblPage()
        {
            Close();
        }

        public ImageViewingParams Ivp
        {
            get { return Book.GetIvp(Filename); }
            set { Book.SetIvp(Filename, value); }
        }

        public void SetInfo(FileInfoEx info)
        {
            lock (_lock)
            {
                Path = info.FullName;
                Filename = info.Name;
                Size = info.Length;
                CreationTime = info.CreationTime;
                LastAccessTime = info.LastAccessTime;
                LastWriteTime = info.LastWriteTime;
            }
        }

        EventHandler _bitmapLoadedHandler = null;
        public async void LoadImageAsync(int priority, EventHandler BitmapLoaded = null, CancellationTokenSource cancel = null)
        {
            if (BitmapLoaded != null)  _bitmapLoadedHandler = BitmapLoaded;
            if ((IsImageLoaded && BitmapLoaded == null) || IsImageLoading)
            {
                if(_dataLoaderTask.Status == TaskStatus.RanToCompletion && BitmapLoaded != null)
                {
                    try
                    {
                        Application.Current.Dispatcher.InvokeIfRequired(DispatcherPriority.Normal, new Action(() =>
                        {
                            if (_bitmapLoadedHandler != null) _bitmapLoadedHandler(this, EventArgs.Empty);
                        }));
                    }
                    catch (Exception) { }
                    finally { _bitmapLoadedHandler = null; }
                }
                return;
            } 
            
            lock (_lock) { _cancelImageLoad = (cancel != null) ? cancel : new CancellationTokenSource(); }
            _dataLoaderTask = BblTask.Run(() => Book.LoadPageData(Index, _cancelImageLoad.Token), priority, _cancelImageLoad.Token);
            try
            {
                await _dataLoaderTask;
                if (_bitmapLoadedHandler != null)
                {
                    try {
                        Application.Current.Dispatcher.InvokeIfRequired(DispatcherPriority.Normal, new Action(() =>
                        {
                            if(_bitmapLoadedHandler != null) _bitmapLoadedHandler(this, EventArgs.Empty);
                        }));
                    }
                    catch (Exception) { }
                    finally { _bitmapLoadedHandler = null; }
                }
                try {
                    if (IsThumbnailLoaded == true && ThumbnailLoaded != null)
                        Application.Current.Dispatcher.InvokeIfRequired(DispatcherPriority.Normal, new Action(() =>
                        {
                            if(ThumbnailLoaded != null) ThumbnailLoaded(this, new EventArgs());
                        }));
                }
                catch { }
            }
            catch
            {
                //if(!(e is InvalidOperationException && e.HResult == -2146233079))
                lock (_lock)
                {
                    if (Image != null) Image.Dispose();
                    Image = null;
                }
            }
            finally
            {
                lock (_lock)
                {
                    _dataLoaderTask = null;
                    if (cancel == null)
                    {
                        _cancelImageLoad.Dispose();
                        _cancelImageLoad = null;
                    }

                }
            }
        }


        async void ReleaseImage()
        {
            if (IsImageLoading && _cancelImageLoad != null)
            {
                try
                {
                    _cancelImageLoad.Cancel();
                    await _dataLoaderTask;
                }
                catch { }
            }
            if (Image != null) Image.Dispose();
            Image = null;
        }

        public async void ReleaseThumbnail()
        {
            Thumbnail = null;
            if (IsImageLoading && _cancelImageLoad != null)
            {
                try
                {
                    _cancelImageLoad.Cancel();
                    await _dataLoaderTask;
                }
                catch { }
            }
            Thumbnail = null;
        }

        public void ReleaseAllData()
        {
            if (Thumbnail != null) ReleaseThumbnail();
            if (Image != null) ReleaseImage();
        }
        public void Close()
        {
            ReleaseAllData();
            if(ThumbnailLoaded != null) foreach (Delegate d in ThumbnailLoaded.GetInvocationList()) ThumbnailLoaded -= (EventHandler)d;
        }

        public int CompareTo(object o)
        {
            var other = (BblPage)o;
            if (Book.Type == BblBook.BookType.Directory)
            {
                string x = this.Filename;
                string y = other.Filename;
                return Utils.SafeNativeMethods.StrCmpLogicalW(x, y);
            }
            else
            {
                string x = this.Path;
                string y = other.Path;
                return Utils.SafeNativeMethods.StrCmpLogicalW(x, y);
            }
        }

        public bool CanDeleteFile => File.Exists(Path);
        public bool DeleteFile()
        {
            if (CanDeleteFile) 
            {
                RecycleBin.DeleteFileOrFolder(Path);
                return true;
            }
            return false;
        }

        public bool CanRenameFile => File.Exists(Path);
        public bool RenameFile(string newName, bool silent=false)
        {
            if (Filename == newName) return true;
            if (CanRenameFile) 
            {
                string newPath = System.IO.Path.GetDirectoryName(Path) + "\\" + newName;
                if (File.Exists(newPath))
                {
                    if(!silent) MessageBox.Show("Destination File already exists!", "Rename Failure", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation);
                    return false;
                }
                try
                {
                    File.Move(Path, newPath);
                    return true;
                }
                catch (Exception e)
                {
                    if (!silent) MessageBox.Show(e.Message, "Rename Failure", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation);
                }
            }
            return false;
        }

        public bool CanMoveFile => File.Exists(Path);
        public bool MoveFile(string parentFolder)
        {
            if(CanMoveFile)
            {
                string newPath = parentFolder + "\\" + Filename;
                File.Move(Path, newPath);
                return true;
            }
            return false;
        }
    }
}
