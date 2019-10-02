using Bubbles3.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Archives;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace Bubbles3.Models
{
    public partial class BblBookArchive : BblBook
    {
        
        BblArchive _archive;
        public BblBookArchive(BblLibraryRootNode root, BblLibraryNode parent, WIN32_FIND_DATA findData) : base(root, parent, findData, BookType.Archive)
        {
        }
        public BblBookArchive(BblLibraryRootNode root, BblLibraryNode parent, FileSystemInfoEx info) : base(root, parent, info, BookType.Archive)
        {
        }


        protected override void Populate()
        {
            try
            {
                _cancelPopulateTask.Token.ThrowIfCancellationRequested();
                _archive = new BblArchive(this);
                if( _pages == null )
                {
                    ObservableCollection<BblPage> pages = _archive.GetPagesList();
                    _cancelPopulateTask.Token.ThrowIfCancellationRequested();
                    if (pages.Count <= 0) return;
                    lock (_lock) { _pages = pages; }
                }
            }
            catch { UnPopulate(); }
        }

        protected override void UnPopulate()
        {
            base.UnPopulate();
            if(_archive != null)
            { 
                _archive.Dispose();
                _archive = null;
            }
        }

        public override void LoadPageData(int index, CancellationToken cancel)
        {
            if (_pages == null ) return;
            var p = _pages[index];

            try
            {
                cancel.ThrowIfCancellationRequested();
                if (p.Image == null)
                {
                    var img = _archive.GetPageData(p);
                    lock (p._lock) {
                        p.Image = new BblImgSource( img);
                    }
                }
                try { 
                    if(p.Thumbnail == null)
                    {
                        var t = p.Image.GetThumbnail(128, 128);
                        lock (p._lock) { p.Thumbnail = t; }
                    }
                }
                catch { p.Thumbnail = null; }
            }
            catch
            {
                lock(p._lock)
                { 
                    if (p.Image != null) p.Image.Dispose();
                    p.Image = null;
                }
            }
        }
        
        public override BblLibraryNode OnFileSystemEntryRenamed(FileSystemInfoEx newInfo)
        {
            string oldPath = Path;
            string oldIvpPath = IvpPath;
            base.OnFileSystemEntryRenamed(newInfo);
            if (File.Exists(oldIvpPath)) File.Move(oldIvpPath, IvpPath);
            if(_pages != null)
                foreach (var p in _pages)
                {
                    lock(p._lock)
                    {
                        p.Path = p.Path.Replace(oldPath, newInfo.FullName);
                        p.LastAccessTime = LastAccessTime;
                        p.LastWriteTime = LastWriteTime;
                        p.CreationTime = CreationTime;
                    }
                    
                }

            if (!_demoted) Application.Current.Dispatcher.BeginInvokeIfRequired(DispatcherPriority.Background, new Action(() => { FireRenamed(); }));
            return this;
        }

        public override void OnFileSystemEntryDeleted()
        {
            string ivpPath = IvpPath;
            if (!_demoted) Application.Current.Dispatcher.BeginInvokeIfRequired(DispatcherPriority.Background, new Action(() => { FireDeleted(); }));
            Root.OnBookOperation(new BookOperationData(this, BookOperations.Remove, null));
            base.OnFileSystemEntryDeleted();
            if (File.Exists(ivpPath)) File.Delete(ivpPath);
        }


    }
}
