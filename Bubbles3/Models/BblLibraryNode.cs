using Bubbles3.Utils;
using System;
using System.Collections.Generic;
using System.IO;

namespace Bubbles3.Models
{
    public class BblLibraryNode
    {
        public static string[] imageFileExtensions = { ".jpg", ".jpeg", ".gif", ".png", ".bmp", ".tiff", ".tif" };
        public static string[] archiveFileExtensions = { ".rar", ".cbr", ".zip", ".cbz" };
        public static string[] pdfFileExtensions = { ".pdf" };
        public static string[] bookDirectoryExtensions = { ".book", ".zine" };


        public BblLibraryRootNode Root { get; protected set; }
        public virtual bool IsRoot => false;
        public virtual bool IsBook => false;
        public BblLibraryNode Parent { get; private set; }

        public string Path { get; private set; }

        public string Name { get; private set; }

        public DateTime CreationTime { get; private set; }

        public DateTime LastAccessTime { get; private set; }

        public DateTime LastWriteTime { get; private set; }


        public FileAttributes Attributes { get; private set; }

        public bool IsFolder => ((Attributes & FileAttributes.Directory) == FileAttributes.Directory);

        public long Length { get; private set; }

        public string Extension => System.IO.Path.GetExtension(Path);

        List<BblLibraryNode> _children;
        public List<BblLibraryNode> Children => _children;

        public bool Exists
        {
            get
            {
                if (IsFolder) return Directory.Exists(Path);
                else return File.Exists(Path);
            }
        }

        protected object _lock = new Object();
        public object NodeLock => _lock;

        #region Constructors
        protected BblLibraryNode(DirectoryInfoEx libraryRoot)
        {
            LoadInfo(libraryRoot);
        }

        protected BblLibraryNode(BblLibraryRootNode root, BblLibraryNode parent, WIN32_FIND_DATA findData)
        {
            Root = root;
            Parent = parent;
            LoadInfo(parent.Path, findData);
        }
        protected BblLibraryNode(BblLibraryRootNode root, BblLibraryNode parent, FileSystemInfoEx info)
        {
            Root = root;
            Parent = parent;
            LoadInfo(info);
        }
        protected BblLibraryNode(BblLibraryNode node) // copy constructor
        {
            Root = node.Root;
            Parent = node.Parent;
            LoadInfo(node);
            _children = node.Children;
        }
        #endregion

        #region LoadInfo
        protected void LoadInfo(BblLibraryNode info)
        {
            lock(_lock)
            { 
                Path = info.Path;
                Name = info.Name;
                Attributes = info.Attributes;
                CreationTime = info.CreationTime;
                LastAccessTime = info.LastAccessTime;
                LastWriteTime = info.LastWriteTime;
                Length = (info.IsFolder) ? 0 : info.Length;
            }
        }
        protected void LoadInfo(FileSystemInfoEx info)
        {
            lock (_lock)
            {
                Path = info.FullName;
                Name = info.Name;
                Attributes = info.Attributes;
                CreationTime = info.CreationTime;
                LastAccessTime = info.LastAccessTime;
                LastWriteTime = info.LastWriteTime;
                Length = (info is FileInfoEx) ? (info as FileInfoEx).Length : 0;
            }
        }
        protected void LoadInfo(string directoryPath, WIN32_FIND_DATA findData)
        {
            lock (_lock)
            {
                Path = System.IO.Path.Combine(directoryPath, findData.cFileName);
                Name = findData.cFileName;
                Attributes = findData.dwFileAttributes;
                CreationTime = findData.CreationTime;
                LastAccessTime = findData.LastAccessTime;
                LastWriteTime = findData.LastWriteTime;
                Length = findData.Length;
            }
        }
        #endregion

        #region Inflate
        public virtual void Inflate()
        {
            EnumerateChildren();
            if (_children != null) foreach (var c in _children) c.Inflate();
        }

        protected void EnumerateChildren()
        {
            DirectoryEntries files = new DirectoryEntries(this.Path, "*");
            if(Path == "D:\\pix\\mixes weekly\\Nouveau dossier")
            {

            }
            bool img = false;
            using (FastDirectoryEnumerator e = (FastDirectoryEnumerator)files.GetEnumerator())
            {
                while (e.MoveNext())
                {
                    var data = e.Current;
                    if (data.cFileName == "." || data.cFileName == "..") continue;
                    string path = System.IO.Path.Combine(Path, data.cFileName);

                    bool isfolder = ((data.dwFileAttributes & FileAttributes.Directory) == FileAttributes.Directory);
                    string ext = System.IO.Path.GetExtension(path);
                    if (isfolder)
                    {
                        if (IsBblBookDirectoryExtension(ext)) AddChild(new BblBookDirectory(Root, this, data));
                        else AddChild(new BblLibraryNode(Root, this, data));
                    }
                    else
                    {
                        if (!img && IsImageFileExtension(ext))
                        {
                            var lastWrite = data.LastWriteTime;
                            LastWriteTime = lastWrite;
                            if(!IsBook)Root.AddPromotable(this);
                            img = true;
                            FirstImage = path;
                        }
                        else if (IsArchiveFileExtension(ext))
                        {
                            AddChild(new BblBookArchive(Root, this, data));
                        }
                        else if (IsPDF(ext))
                        {
                            AddChild(new BblBookPdf(Root, this, data));
                        }
                    }
                }
            }
            if (!IsBook && IsFolder && (_children == null || _children.Count == 0))
            {
                
            }
        }
        #endregion

        #region find children nodes
        public BblLibraryNode FindChild(FileSystemInfoEx info)
        {
            if (!IsRoot) return null;
            if (info == null) return null;
            Stack<FileSystemInfoEx> pathstack = BuildPathStack(info, this);
            if (pathstack == null || pathstack.Count == 0) return null;
            return FindChild(pathstack);
        }
        protected BblLibraryNode FindChild(Stack<FileSystemInfoEx> pathstack)
        {
            if (pathstack == null || pathstack.Count == 0 || _children == null || _children.Count == 0) return null;

            FileSystemInfoEx childpath = pathstack.Pop();
            if (childpath.FullName == Path) return this;
            foreach (var child in _children)
            {
                if (child.Path == childpath.FullName)
                {
                    if (pathstack.Count == 0) return child;
                    else return child.FindChild(pathstack);
                }
            }
            return null;
        }
        public void GetAllBooks(ref List<BblBook> books)
        {
            if (books == null) return;
            if (this is BblBook) books.Add(this as BblBook);
            if(_children!= null) foreach (var c in _children) c.GetAllBooks(ref books);
        }
        #endregion

        #region Add, Remove, Replace Children
        private void AddChild(BblLibraryNode child)
        {
            lock(_lock)
            { 
                if (_children == null) _children = new List<BblLibraryNode>();
                _children.Add(child);
            }
        }

        public BblLibraryNode AddChildDirectory(DirectoryInfoEx info)
        {
            if (IsBblBookDirectoryExtension(System.IO.Path.GetExtension(info.FullName))) return new BblBookDirectory(Root, this, info);
            else
            {
                BblLibraryNode node = new BblLibraryNode(Root, this, info);
                node.Inflate();
                AddChild(node);
                return node;
            }
        }

        public virtual BblLibraryNode AddChildFile(FileInfoEx info)
        {
            if (info.Parent.FullName != Path) return null;
            if(IsArchiveFileExtension(info.Extension)|| IsPDF(info.Extension))
            {
                BblLibraryNode b = null;
                if (IsArchiveFileExtension(info.Extension)) b = new BblBookArchive(Root, this, info);
                else if (IsPDF(info.Extension)) b = new BblBookPdf(Root, this, info);
                AddChild(b);
                return b;
            }
            else if(IsImageFileExtension(info.Extension) && !IsBook && IsFolder && (_children == null || _children.Count==0))
            {
                Root.AddPromotable(this);
            }

            return null;
        }
        
        private void ReplaceChild(BblLibraryNode oldChild, BblLibraryNode newChild)
        {
            lock(_lock)
            {
                int idx = _children.IndexOf(oldChild);
                if (idx == -1) throw new Exception("Replaced Child Not Found in BblLibraryNode.ReplaceChild");
                else _children[idx] = newChild;
            }
        }

        public virtual bool DeleteChild(BblLibraryNode n)
        {
            lock (_lock)
            { 
                bool retval = _children.Remove(n);
                if (_children.Count == 0) _children = null;
                return retval;
            }
        }

        public virtual bool DeleteChildFile(FileInfoEx f)
        {
            foreach (var c in _children)
                if (c.Path == f.FullName)
                {
                    c.OnFileSystemEntryDeleted();
                    return DeleteChild(c);
                }
            return false;
        }
        #endregion

        public virtual void OnFileSystemEntryDeleted()
        {
            List<BblBook> books = new List<BblBook>();
            GetAllBooks(ref books);
            foreach (var b in books)
            {
                Root.OnBookOperation(new BookOperationData(b, BookOperations.Remove, null));
                Root.BookCount--;
            }

            if (Parent != null) Parent.DeleteChild(this);
        }



        public virtual BblLibraryNode OnFileSystemEntryRenamed(FileSystemInfoEx newInfo)
        {
            //this bit only deals with plain BblLibraryNodes, overriding in all three book classes is mandatory.
            if(_children != null)
                foreach (var c in _children)
                {
                    string newPath = c.Path.Replace(Path, newInfo.FullName);
                    if (c.IsFolder) c.OnFileSystemEntryRenamed(new DirectoryInfoEx(newPath));
                    else c.OnFileSystemEntryRenamed(new FileInfoEx(newPath));
                }

            LoadInfo(newInfo);

            if ( IsBblBookDirectoryExtension( System.IO.Path.GetExtension(newInfo.FullName)) && (this is BblBookDirectory) == false)
            {
                //swapping BblLibraryNode with BblBookDirectory.
                BblBookDirectory b = new BblBookDirectory(this);
                Parent.ReplaceChild(this, b);
                return b;
            }


            return this;
        }

        public void PromoteToBookDirectory()
        {
            try
            { 
                Directory.Move(Path, Path + ".book");
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public string FirstImage { get; private set; }

        #region static methods
        /// <summary>
        /// Build the pathstack required to run the FindChild method
        /// </summary>
        protected static Stack<FileSystemInfoEx> BuildPathStack(FileSystemInfoEx path, BblLibraryNode root)
        {
            Stack<FileSystemInfoEx> pathstack = new Stack<FileSystemInfoEx>();

            if (path.FullName == root.Path)
            {
                pathstack.Push(path);
                return pathstack;
            }

            while (path != null)
            {
                pathstack.Push(path);
                try { path = path.Parent; }
                catch { return null; }

                if (path.FullName == root.Path) return pathstack;
                else if (path == null) return null;
            }
            return null;
        }

        

        protected static bool IsImageDirectory(string path)
        {
            bool hasImages = false;
            if(Directory.Exists(path))
            { 
                var files = new DirectoryEntries(path, "*");
                using (var e = files.GetEnumerator())
                {
                    while(e.MoveNext())
                    {
                        var data = e.Current;
                        if (data.cFileName == "." || data.cFileName == "..") continue;
                        string filepath = System.IO.Path.Combine(path, data.cFileName);
                        bool isfolder = ((data.dwFileAttributes & FileAttributes.Directory) == FileAttributes.Directory);
                        string ext = System.IO.Path.GetExtension(filepath);
                        if (! isfolder && IsImageFileExtension(ext))
                        {
                            hasImages = true;
                            break;
                        }
                    }
                }
            }
            return hasImages;
        }

        public static bool IsArchiveFileExtension(String ext)
        {
            ext = ext.ToLowerInvariant();
            foreach (string ax in archiveFileExtensions) if (ax == ext) return true;
            return false;
        }

        public static bool IsImageFileExtension(String ext)
        {
            ext = ext.ToLowerInvariant();
            foreach (string ix in imageFileExtensions) if (ix == ext) return true;
            return false;
        }
        public static bool IsPDF(String ext)
        {
            ext = ext.ToLowerInvariant();
            foreach (string px in pdfFileExtensions) if (px == ext) return true;
            return false;
        }
        public static bool IsBblBookDirectoryExtension(string ext)
        {
            ext = ext.ToLowerInvariant();
            foreach (string bx in bookDirectoryExtensions) if (bx == ext) return true;
            return false;
        }


        #endregion

        
    }
}
