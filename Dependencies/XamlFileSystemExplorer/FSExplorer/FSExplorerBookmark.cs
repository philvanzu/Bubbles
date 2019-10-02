using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace XamlFSExplorer
{
    public class FSExplorerBookmarkRoot : FSExplorerBookmark
    {
        public FSExplorerBookmarkRoot(FSExplorer explorer, int rootId): base(null, explorer, rootId, null          )
        {
            Name = "Bookmarks";
            _icon = BitmapFrame.Create(new Uri("pack://application:,,,/XamlFSExplorer;component/icons/folder-bookmark.png"));
            NotifyPropertyChanged("Visibility");
            Children = new System.Collections.ObjectModel.ObservableCollection<FSExplorerItem>();
        }
        
        public Object userData;


        public override Visibility Visibility
        {
            get => (Children != null && Children.Count > 0)? Visibility.Visible : Visibility.Collapsed;
        }

        public FSExplorerBookmark AddBookmark(object userData)
        {
            var child = new FSExplorerBookmark(this, Explorer, RootId, userData);
            Children.Add(child);
            NotifyPropertyChanged("Visibility");
            IsExpanded = true;
            return child;
        }
        public void DeleteChild(FSExplorerBookmark child)
        {
            Children.Remove(child);
        }
        public override bool AllowDrop => (Children.Count > 0);
        public override bool CanDoFileOperations => false;

    }



    public class FSExplorerBookmark : FSExplorerItem
    {
        public object UserData { get; set; }
        public event EventHandler BookmarkClicked;
        public event EventHandler BookmarkDeleted;

        FSExplorerBookmarkRoot _root;
        public FSExplorerBookmark(FSExplorerBookmarkRoot parent, FSExplorer explorer, int rootId, object userData) : base()
        {
            _explorer = explorer;
            _root = parent;
            RootId = rootId;
            UserData = userData;
            if(userData != null)Name = userData.ToString();
            _icon = BitmapFrame.Create(new Uri("pack://application:,,,/XamlFSExplorer;component/icons/user-bookmarks.png"));
        }

        public override bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (BookmarkClicked != null) BookmarkClicked(this, EventArgs.Empty);
                Explorer.BookmarkSelected();
            }
        }

        public override bool IsEnabled => true;
        public override bool AllowDrop => false;
        string _name;
        public override string Name
        {
            get => _name;
            set
            {
                _name = value;
                NotifyPropertyChanged("Name");
            }
        }

        public override bool CanDoFileOperations => true;
        public override void Delete()
        {
            if (BookmarkDeleted != null) BookmarkDeleted(this, EventArgs.Empty);
            _root.DeleteChild(this);
        }

    }
}
