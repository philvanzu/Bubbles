using Bubbles3.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
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
    public class BblBookDirectory : BblBook
    {
        

        public BblBookDirectory(BblLibraryRootNode root, BblLibraryNode parent, WIN32_FIND_DATA findData) : base(root, parent, findData, BookType.Directory)
        {

        }
        public BblBookDirectory(BblLibraryRootNode root, BblLibraryNode parent, FileSystemInfoEx info) : base(root, parent, info, BookType.Directory)
        {

        }
        public BblBookDirectory(BblLibraryNode swap) : base(swap, BookType.Directory)
        {

        }

        protected override string IvpPath => Path + "\\.ivp";
        protected override void Populate()
        {
            try
            {
                if(_pages == null)
                {
                    DirectoryEntries files = new DirectoryEntries(this.Path, "*");
                    var pages = new List<BblPage>();
                    using (FastDirectoryEnumerator fde = (FastDirectoryEnumerator)files.GetEnumerator())
                    {

                        while (fde.MoveNext())
                        {
                            _cancelPopulateTask.Token.ThrowIfCancellationRequested();

                            var data = fde.Current;
                            if ((data.dwFileAttributes & FileAttributes.Directory) == FileAttributes.Directory) continue;
                            string path = System.IO.Path.Combine(Path, data.cFileName);
                            string ext = System.IO.Path.GetExtension(path);
                            if (IsImageFileExtension(ext))
                            {
                                BblPage page = new BblPage(this);
                                page.Filename = data.cFileName;
                                page.Path = System.IO.Path.Combine(Path, data.cFileName);
                                page.Size = data.Length;
                                page.CreationTime = data.CreationTime;
                                page.LastAccessTime = data.LastAccessTime;
                                page.LastWriteTime = data.LastWriteTime;


                                lock (_lock) { pages.Add(page); }
                            }
                        }
                    }
                    if (pages.Count == 0) return;
                    pages.Sort();
                    lock (_lock) { _pages = new ObservableCollection<BblPage>(pages); }
                }
            }
            catch { UnPopulate(); }
        }

        public override void LoadPageData(int index, CancellationToken cancel)
        {
            if (_pages == null) return;
            var p = _pages[index];

            try
            {
                cancel.ThrowIfCancellationRequested();
                if (p.Image == null)
                {
                    lock (p._lock)
                    {
                        using (var fs = new FileStream(p.Path, FileMode.Open, FileAccess.Read))
                        {
                            MemoryStream stream = new MemoryStream((int)fs.Length);
                            fs.CopyTo(stream);
                            lock (p._lock) { p.Image = new BblImgSource( stream); }
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
            catch
            {
                lock (p._lock)
                {
                    if (p.Image != null) p.Image.Dispose();
                    p.Image = null;
                }
            }
        }

        /// this runs on a FileSystemWatcher thread
        public override BblLibraryNode OnFileSystemEntryRenamed(FileSystemInfoEx newInfo)
        {
            if(!_demoted && !IsBblBookDirectoryExtension( newInfo.Extension))
            {
                Root.OnBookOperation(new BookOperationData(this, BookOperations.Remove, null));
                Application.Current.Dispatcher.BeginInvokeIfRequired(DispatcherPriority.Normal, new Action(() => { FireDemoted(); }));
                _demoted = true;
            }
            else if(_demoted && IsBblBookDirectoryExtension(newInfo.Extension))
            {
                Root.OnBookOperation(new BookOperationData(this, BookOperations.Add, null));
                _demoted = false;
            }

            if(Children != null)
                foreach (var c in Children)
                {
                    string newPath = c.Path.Replace(Path, newInfo.FullName);
                    if (c.IsFolder) c.OnFileSystemEntryRenamed(new DirectoryInfoEx(newPath));
                    else c.OnFileSystemEntryRenamed(new FileInfoEx(newPath));
                }

            String oldPath = Path;
            LoadInfo(newInfo);
            if (_pages != null)
            {
                var pArr = _pages.ToArray();
                foreach (var p in pArr)
                {
                    string newPath = p.Path.Replace(oldPath, newInfo.FullName);
                    FileInfoEx info = new FileInfoEx(newPath);
                    if(info.Exists) p.SetInfo(info);
                }
            }
            if (!_demoted) Application.Current.Dispatcher.BeginInvokeIfRequired(DispatcherPriority.Normal, new Action(() => { FireRenamed(); }));
            return this;
        }
        
        /// this runs on a FileSystemWatcher thread
        /// folder has been deleted
        public override void OnFileSystemEntryDeleted()
        {
            Application.Current.Dispatcher.BeginInvokeIfRequired( DispatcherPriority.Normal, new Action(() => { FireDeleted(); }));
            Root.OnBookOperation(new BookOperationData(this, BookOperations.Remove, null));
            //base method removes this and all children books from the library Books list, GAC otw.
            base.OnFileSystemEntryDeleted();
        }
        
        /// this runs on a FileSystemWatcher thread
        /// Inserts a new page, preserving sort order.
        public override BblLibraryNode AddChildFile(FileInfoEx info)
        {
            if (IsImageFileExtension(info.Extension))
            {
                if (_demoted)
                {
                    _demoted = false;
                    Root.OnBookOperation(new BookOperationData(this, BookOperations.Add, null));
                }
                else if (IsPopulated)
                {
                    BblPage p = new BblPage(this);
                    p.SetInfo(info);
                    string name = info.Name;
                    Application.Current.Dispatcher.BeginInvokeIfRequired(
                        DispatcherPriority.Normal,
                        new Action(() => {
                            lock (_lock)
                            {
                                int idx = 0;
                                for (; idx < _pages.Count; idx++)
                                    if (Utils.SafeNativeMethods.StrCmpLogicalW(_pages[idx].Filename, name) >= 0) break;
                                _pages.Insert(idx, p);
                            } }
                        ));
                }
                //string pop = (IsPopulated) ? "Pop ||" : "NoPop ||";
                //string exist = (info.Exists) ? "exists" : "no exist";
                //string s = info.Name + " processed! ||" + pop + exist;
                //Console.WriteLine(s);
                return this;
            }
            else  return base.AddChildFile(info);
        }
        
        /// this runs on a FileSystemWatcher thread
        /// Deletes a page, not the associated file. 
        /// TODO: Maybe this name is confusing. It is less so in the base class.
        public override bool DeleteChildFile(FileInfoEx f)
        {
            if (IsPDF(f.Extension) || IsArchiveFileExtension(f.Extension)) return base.DeleteChildFile(f);
            else if (IsImageFileExtension(f.Extension) && IsPopulated)
            {
                var page = _pages.Where(x => x.Path == f.FullName).FirstOrDefault();
                if (page == null) return false;
                Application.Current.Dispatcher.BeginInvokeIfRequired( 
                    DispatcherPriority.Normal,
                    new Action(() =>
                    {
                        lock (_lock)
                        {
                            if(_ivps != null) _ivps.Remove(page.Filename);
                            if(_pages != null)_pages.Remove(page);
                        }
                    } ));
            }
            return true;
        }
        


        // This runs on FileSystemWatcher thread
        public bool OnPageFileRenamed(FileInfoEx oldPath, FileInfoEx newPath)
        {
            ImageViewingParams ivp = new ImageViewingParams();
            ivp.Reset();
            BblPage oldPage = null;
            BblPage newPage = null;
            if (IsPopulated)
            {
                oldPage = _pages.Where(x => x.Path == oldPath.FullName).FirstOrDefault();
                ivp = oldPage.Ivp;
                newPage = new BblPage(this);
                newPage.SetInfo(newPath);


                Application.Current.Dispatcher.BeginInvokeIfRequired(
                    DispatcherPriority.Normal,
                    new Action(() =>
                    {
                        lock (_lock) {
                            _pages.Remove(oldPage);
                            int idx = 0;
                            for (; idx < _pages.Count; idx++)
                                if (Utils.SafeNativeMethods.StrCmpLogicalW(_pages[idx].Filename, newPath.Name) >= 0)
                                    break;
                            _pages.Insert(idx, newPage);
                        }
                    }));
            }


            if ( (ivp != null && ivp.isDirty) || (_ivps != null && _ivps.TryGetValue(oldPath.Name, out ivp)) )
            {
                ivp.filename = newPath.Name;
                if(newPage != null) lock(newPage._lock) newPage.Ivp = ivp;
                if (_ivps != null)
                {
                    _ivps.Remove(oldPath.Name);
                    if(!_ivps.ContainsKey(newPath.Name))
                    { 
                        try  { _ivps.Add(newPath.Name, ivp); }
                        catch(System.ArgumentException e)
                        {
                            if ((UInt32)e.HResult != 0x80070057) Console.WriteLine(e.Message);
                        }
                        catch(Exception x) { Console.WriteLine(x.Message); }
                    }
                }
            }

            return false;
        }
        public override bool CanDeleteFile => Directory.Exists(Path);
        public override bool DeleteFile(bool silent = false)
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
                    if (!silent) System.Windows.MessageBox.Show(e.Message, "Thrash Directory Failure", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation);
                }
                
            }
            return false;
        }
        public override bool CanMoveFile => File.Exists(Path);
        public override bool MoveFile(string parentDirectory, bool silent = false)
        {
            if (CanMoveFile)
            {
                string newPath = parentDirectory + "\\" + Name;
                try
                {
                    Directory.Move(Path, newPath);
                    return true;
                }
                catch (Exception e)
                {
                    if (!silent) System.Windows.MessageBox.Show(e.Message, "Move Directory Failure", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation);
                }
            }
            return false;
        }
        public override bool CanRenameFile => Directory.Exists(Path);
        public override bool RenameFile(string newName, bool silent = false)
        {
            if (CanRenameFile)
            {
                string newPath = System.IO.Path.GetDirectoryName(Path)+ "\\" + newName;
                try
                {
                    Directory.Move(Path, newPath);
                    return true;
                }
                catch (Exception e)
                {
                    if (!silent) System.Windows.MessageBox.Show(e.Message, "Rename Directory Failure", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation);
                }
                
            }
            return false;
        }
    }
}
