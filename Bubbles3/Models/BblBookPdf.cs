using Bubbles3.Utils;
using PdfiumLight;
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
using XamlFSExplorer.Utils;

namespace Bubbles3.Models
{
    public class BblBookPdf:BblBook
    {

        public BblBookPdf(BblLibraryRootNode root, BblLibraryNode parent, WIN32_FIND_DATA findData) : base(root, parent, findData, BookType.Pdf)
        {

        }
        public BblBookPdf(BblLibraryRootNode root, BblLibraryNode parent, FileSystemInfoEx info) : base(root, parent, info, BookType.Pdf)
        {

        }

        public override BblLibraryNode OnFileSystemEntryRenamed(FileSystemInfoEx newInfo)
        {
            string oldPath = Path;
            string oldIvpPath = IvpPath;
            base.OnFileSystemEntryRenamed(newInfo);
            if (File.Exists(oldIvpPath)) File.Move(oldIvpPath, IvpPath);
            if (_pages != null)
                foreach (var p in _pages)
                {
                    lock (p._lock)
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

        protected override void Populate()
        {
            try
            {
                _cancelPopulateTask.Token.ThrowIfCancellationRequested();
                if (_pages == null)
                {
                    int count;
                    var pages = new List<BblPage>();
                    using (PdfDocument doc = new PdfDocument(Path))
                    {
                        count = doc.PageCount();
                    }
                    if (count >= 0)
                    { 
                        for (int i = 0; i < count; i++)
                        {
                            _cancelPopulateTask.Token.ThrowIfCancellationRequested();
                            BblPage p = new BblPage(this);
                            p.Filename = "page " + (i + 1).ToString();
                            p.Size = 0;
                            p.LastAccessTime = LastAccessTime;
                            p.LastWriteTime = LastWriteTime;
                            p.CreationTime = CreationTime;
                            p.Path = Path + "::_" + (i + 1).ToString();
                            pages.Add(p);
                        }
                    }
                    else return;
                    lock (_lock) { _pages = new ObservableCollection<BblPage>(pages); }
                }
            }
            catch { UnPopulate(); }
        }

        const float maxPageDimension = 921.6f;

        public override void LoadPageData(int index, CancellationToken cancel)
        {
            if (_pages == null) return;
            var p = _pages[index];

            try {
                cancel.ThrowIfCancellationRequested();
                if(p.Image == null)
                { 
                    using (PdfDocument doc = new PdfDocument(Path))
                    {
                        using (var pdfPage = doc.GetPage(index))
                        {
                            float w, h;
                            float ratio = (float)pdfPage.Height / (float)pdfPage.Width;
                            if( (ratio >= 1 && pdfPage.Height <= maxPageDimension) || (ratio < 1 && pdfPage.Width <= maxPageDimension))
                            {
                                w = (float)pdfPage.Width;
                                h = (float)pdfPage.Height;
                            }
                            else
                            {
                                h = (ratio > 1) ? maxPageDimension : maxPageDimension * ratio;
                                w = (ratio < 1) ? maxPageDimension : maxPageDimension / ratio;
                            }

                            using (var img = pdfPage.Render((int)Math.Round(w * 4.16f), (int)Math.Round(h * 4.16f))) //pdfPage.Size is in pts @ 72dpi | target is 300dpi | 300/72 = 4.16
                            {
                                var stream = new MemoryStream();
                                img.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                                p.Image = new BblImgSource(stream);
                            }
                        }
                    }
                }
                try
                {
                    if (p.Thumbnail == null)
                    {
                        var t = p.Image.GetThumbnail(128, 128);
                        lock (p._lock) { p.Thumbnail = t; }
                    }
                }
                catch
                {
                    lock (p._lock)
                    {
                        p.Thumbnail = null;
                    }
                }
                cancel.ThrowIfCancellationRequested();
            }
            catch(Exception)
            {
                lock(p._lock)
                { 
                    if (p.Image != null) p.Image.Dispose();
                    p.Image = null;
                }
            }
        }
    }
}
