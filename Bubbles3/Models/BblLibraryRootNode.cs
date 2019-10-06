using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bubbles3.Models
{
    public class BblLibraryRootNode:BblLibraryNode
    {
        public delegate void BookOperationDelegate(BookOperationData data);
        public BookOperationDelegate OnBookOperation;

        public override bool IsRoot => true;

        public int BookCount { get; set; }
        public int BookIndex { get; set; }

        public BblLibraryRootNode(DirectoryInfoEx info) : base(info)
        {
            Root = this;
        }

        HashSet<BblLibraryNode> _promotables = new HashSet<BblLibraryNode>();
        public HashSet<BblLibraryNode> Promotables => _promotables;

        //promotables are folders containing images and devoid of the .book extension
        //They could be promoted to BblBookDirectory status when ProcessPromotables is called
        //by renaming them, adding the .book extension.
        public void AddPromotable(BblLibraryNode n)
        {
            //filter out any special folder or folder within the path of an environment variable
            //as those cannot be renamed without fucking something up.
            foreach (string env in Environment.GetEnvironmentVariables().Values)
                if (env.ToLowerInvariant().Contains(n.Path.ToLowerInvariant()))
                    return;

            foreach (Environment.SpecialFolder folder_type in Enum.GetValues(typeof(Environment.SpecialFolder)))
                if (Environment.GetFolderPath(folder_type).ToLowerInvariant().Contains(n.Path.ToLowerInvariant()))
                    return;

            lock (_lock) { _promotables.Add(n); }
        }
        public List<BblLibraryNode> GetPromotables() { return _promotables.ToList(); }
        public override void Inflate()
        {
            base.Inflate();
            //ProcessPromotables();
        }

        public BblLibraryNode AddDirectory(string path)
        {
            BblLibraryNode node = null;
            DirectoryInfoEx d = new DirectoryInfoEx(path);
            BblLibraryNode parent = FindChild(d.Parent);
            if (parent != null) node = parent.AddChildDirectory(d);
            return null;
        }
        public BblLibraryNode RenameDirectory(string newPath, string oldPath)
        {
            DirectoryInfoEx d = new DirectoryInfoEx(newPath);
            DirectoryInfoEx old = new DirectoryInfoEx(oldPath);
            BblLibraryNode node = FindChild(old);
            if (node != null) return node.OnFileSystemEntryRenamed(d);
            return null;
        }
        public bool DeleteDirectory(string path)
        {
            DirectoryInfoEx d = new DirectoryInfoEx(path);
            BblLibraryNode node = FindChild(d);
            if(node != null) node.OnFileSystemEntryDeleted();
            return false;
        }
        public BblLibraryNode AddFile(string path)
        {
            FileInfoEx f = new FileInfoEx(path);
            var ext = f.Extension;
            BblLibraryNode parent = FindChild(f.Parent);
            if (parent != null)
            {
                var n = parent.AddChildFile(f);
                return n;
            }
            return null;
        }

        public BblLibraryNode RenameFile(string newPath, string oldPath)
        {
            var old = new FileInfoEx(oldPath);
            var f = new FileInfoEx(newPath);

            if (IsArchiveFileExtension(old.Extension) || BblBook.IsPDF(old.Extension))
            {
                var node = FindChild(old);
                if (node != null) return node.OnFileSystemEntryRenamed(f);
            }
            else if (IsImageFileExtension(old.Extension))
            {
                var node = FindChild(old.Parent);
                if (node != null && node is BblBookDirectory)
                {
                    (node as BblBookDirectory).OnPageFileRenamed(old, f);
                    return node;
                }
            }
            return null;
        }

        public bool DeleteFile(string path)
        {
            FileInfoEx f = new FileInfoEx(path);
            BblLibraryNode parentNode = FindChild(f.Parent);
            BblLibraryNode node = null;

            if(parentNode?.Children != null)
            { 
                foreach (var n in parentNode.Children)
                { 
                    if ( n.Path == path)
                    {
                        node = n;
                        break;
                    }
                }
            }
            if (parentNode != null && parentNode is BblBookDirectory)
            {
                return parentNode.DeleteChildFile(f);
            }
            else if(node != null )
            {
                node.OnFileSystemEntryDeleted();
            }
            return false;
        }
    }
}
