using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using XamlFSExplorer.Utils;

namespace XamlFSExplorer
{
    public class FSExplorerItem : INotifyPropertyChanged
    {
        protected FSExplorer _explorer;
        public FSExplorer Explorer { get => _explorer; }

        public static FSExplorerItem DummyChild = new FSExplorerItem();
        

        static IconExtractor _iconExtractor = new IconExtractor();
        protected BitmapSource _icon;

        public FileSystemInfoEx Info { get; private set; }
        public int Id { get => (ParentDirectory != null)? ParentDirectory.Children.IndexOf(this) : 0; }

        public int RootId { get; set; }
        //{ get; set; }
        public string Path { get => Info.FullName; }
        string _name;
        public virtual string Name { get => _name; set { _name = value; NotifyPropertyChanged("Name"); } }
        public string LastModified { get => (_isNavup)? string.Empty : Info?.LastWriteTime.ToString("g", CultureInfo.CurrentCulture); }
        public string TypeName { get => (_isNavup) ? "Parent Directory" : Info.TypeName(); }
        public string Size
        {
            get
            {
                if (_isNavup) return string.Empty;

                else if (IsLogicalDriveRoot && LogicalDisk.Disks.TryGetValue(Info.FullName.Substring(0, 2), out LogicalDisk disk))
                {
                    if (disk.size >= 1099511627776) return ((double)disk.size / 1099511627776).ToString("N2", CultureInfo.CurrentCulture) + " To";
                    if (disk.size >= 1073741824) return ((double)disk.size / 1073741824).ToString("N1", CultureInfo.CurrentCulture) + " Go";
                    else return ((double)disk.size / 1048576).ToString("N0", CultureInfo.CurrentCulture) + " Mo";
                }
                else if (Info is FileInfoEx file) return ((double)file.Length / 1024).ToString("N0", CultureInfo.CurrentCulture) + " Ko";

                return string.Empty;
            }
        }
        public BitmapSource Icon { get => _icon; }
        public Image MenuIcon   { get => new Image() { Source = Icon }; }

        public virtual Visibility Visibility
        {
            get
            {
                if (_explorer.ShowDisabled == false && IsEnabled == false) return Visibility.Collapsed;
                if (_explorer.HideAttributes.HasFlag(FileAttributes.Archive) && Info.Attributes.HasFlag(FileAttributes.Archive)) return Visibility.Collapsed;
                if (_explorer.HideAttributes.HasFlag(FileAttributes.Compressed) && Info.Attributes.HasFlag(FileAttributes.Compressed)) return Visibility.Collapsed;
                if (_explorer.HideAttributes.HasFlag(FileAttributes.Device) && Info.Attributes.HasFlag(FileAttributes.Device)) return Visibility.Collapsed;
                if (_explorer.HideAttributes.HasFlag(FileAttributes.Directory) && Info.Attributes.HasFlag(FileAttributes.Directory)) return Visibility.Collapsed;
                if (_explorer.HideAttributes.HasFlag(FileAttributes.Encrypted) && Info.Attributes.HasFlag(FileAttributes.Encrypted)) return Visibility.Collapsed;
                if (_explorer.HideAttributes.HasFlag(FileAttributes.Hidden) && Info.Attributes.HasFlag(FileAttributes.Hidden) && 
                    IsLogicalDriveRoot == false) return Visibility.Collapsed;
                if (_explorer.HideAttributes.HasFlag(FileAttributes.IntegrityStream) && Info.Attributes.HasFlag(FileAttributes.IntegrityStream)) return Visibility.Collapsed;
                if (_explorer.HideAttributes.HasFlag(FileAttributes.Normal) && Info.Attributes.HasFlag(FileAttributes.Normal)) return Visibility.Collapsed;
                if (_explorer.HideAttributes.HasFlag(FileAttributes.NoScrubData) && Info.Attributes.HasFlag(FileAttributes.NoScrubData)) return Visibility.Collapsed;
                if (_explorer.HideAttributes.HasFlag(FileAttributes.NotContentIndexed) && Info.Attributes.HasFlag(FileAttributes.NotContentIndexed)) return Visibility.Collapsed;
                if (_explorer.HideAttributes.HasFlag(FileAttributes.Offline)  && Info.Attributes.HasFlag(FileAttributes.Offline )) return Visibility.Collapsed;
                if (_explorer.HideAttributes.HasFlag(FileAttributes.ReadOnly) && Info.Attributes.HasFlag(FileAttributes.ReadOnly)) return Visibility.Collapsed;
                if (_explorer.HideAttributes.HasFlag(FileAttributes.ReparsePoint) && Info.Attributes.HasFlag(FileAttributes.ReparsePoint)) return Visibility.Collapsed;
                if (_explorer.HideAttributes.HasFlag(FileAttributes.SparseFile) && Info.Attributes.HasFlag(FileAttributes.SparseFile)) return Visibility.Collapsed;
                if (_explorer.HideAttributes.HasFlag(FileAttributes.System) && Info.Attributes.HasFlag(FileAttributes.System) && 
                    IsLogicalDriveRoot == false) return Visibility.Collapsed;
                if (_explorer.HideAttributes.HasFlag(FileAttributes.Temporary) && Info.Attributes.HasFlag(FileAttributes.Temporary)) return Visibility.Collapsed;

                return Visibility.Visible;
            }
            set => NotifyPropertyChanged("Visibility");
        }

    public enum ItemTypes { /*Navup = 0, */LogicalDrive = 2, Folder = 1, File = 3 }
        public ItemTypes ItemType
        {
            get
            {
                if (!IsDirectory) return ItemTypes.File;
                if (IsLogicalDriveRoot) return ItemTypes.LogicalDrive;
                return ItemTypes.Folder;
            }
        }
        public int ItemTypeValue { get { return (int)ItemType; } }

        public FSExplorerItem ParentDirectory { get; set; }
        public bool IsRoot { get => Info.FullName == "::Root"; }
        public bool IsDirectory { get => (Info == null || Info is DirectoryInfoEx); }
                
        public bool IsLogicalDriveRoot { get => Info!=null && Info.FullName.StartsWith("::") == false && System.IO.Path.GetPathRoot(Info.FullName) == Info.FullName; }
        public bool IsFile { get => Info is FileInfoEx; }
        public bool Exists { get => (IsDirectory) ? Directory.Exists(Info.FullName) : (File.Exists(Info.FullName)); }
        public int ChildrenCount { get; private set; }

        public bool HasDummyChild { get; set; }

        protected bool _isExpanded;
        public virtual bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (value != _isExpanded)
                {
                    if (value == true && HasDummyChild)
                    {
                        CreateChildren();
                    }
                    _isExpanded = value;
                    NotifyPropertyChanged("IsExpanded");
                }

            }
        }

        public void CreateChildren()
        {
            if(HasDummyChild)
            { 
                Children = FreshChildren();
                HasDummyChild = false;
                NotifyPropertyChanged("Children");
                NotifyPropertyChanged("Directories");
            }
        }

        protected bool _isSelected;
        public virtual bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (value != _isSelected)
                {
                    if (value == true)
                    {
                        if (HasDummyChild) CreateChildren();
                        _explorer.SelectedTreeItem = this;
                    }
                    else
                    {
                        //_explorer.SelectedTreeItem = null;
                    }
                    _isSelected = value;
                    NotifyPropertyChanged("IsSelected");
                }
            }
        }

        bool _isListSelected;
        public bool IsListSelected
        {
            get => _isListSelected;
            set
            {
                if (value != _isListSelected)
                {
                    if (value == true && !IsNavup)
                        _explorer.SelectedListItem = this;

                    _isListSelected = value;
                    NotifyPropertyChanged("IsListSelected");
                }
            }
        }
        bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (value != _isChecked)
                {
                    if (_explorer.CheckedItems == null) _explorer.CheckedItems = new ObservableCollection<FileSystemInfoEx>();
                    var checkedList = _explorer.CheckedItems;

                    if (value == true) checkedList.Add(Info);
                    else checkedList.Remove(Info);

                    _isChecked = value;
                    NotifyPropertyChanged("IsChecked");
                }
            }
        }

        bool _isNavup;
        internal bool IsNavup
        {
            get =>_isNavup;
            set
            {
                _isNavup = value;
                Name = "..";
                _icon = new BitmapImage(new Uri("pack://application:,,,/XamlFSExplorer;component/icons/parentDirectory.png"));
            }
        }

        public bool IsUnauthorizedAccess { get; set; }
        public virtual bool IsEnabled
        {
            get
            {
                if (IsNavup) return true;
                if (Info.Exists == false) return false;
                if (IsLogicalDriveRoot)
                {
                    LogicalDisk.Disks.TryGetValue(Path.Substring(0, 2), out LogicalDisk disk);
                    if(disk == null || disk.IsEnabled == false) return false;
                }
                if (IsUnauthorizedAccess) return false;
                return true;
            }
            set => NotifyPropertyChanged("IsEnabled");
        }

        private ObservableCollection<FSExplorerItem> _children;
        public ObservableCollection<FSExplorerItem> Children
        {
            get => _children;
            protected set
            {
                _children = value;

                if (_childrenCV != null) _childrenCV.DetachFromSourceCollection();
                _childrenCV = (ListCollectionView)CollectionViewSource.GetDefaultView(_children);
                NotifyPropertyChanged("Children");
                NotifyPropertyChanged("Directories");
                NotifyPropertyChanged("ChildrenCV");
            }
        }

        ListCollectionView _childrenCV;
        public System.Windows.Data.ListCollectionView ChildrenCV => _childrenCV;

        public ObservableCollection<FSExplorerSortField> SortFields => new ObservableCollection<FSExplorerSortField>(Enum.GetValues(typeof(FSExplorerSortField)).Cast<FSExplorerSortField>());

        public ObservableCollection<FSExplorerItem> Directories
        { get {
                if (_children == null) return null;
                var dirs = new ObservableCollection<FSExplorerItem>();
                foreach (var c in _children) if (c.IsDirectory) dirs.Add(c);
                return dirs;
            } }

        public bool IsFresh
        {
            get
            {
                if (!Info.Exists) return false;
                if (IsDirectory)
                {
                    try { 
                        var dir = Info as DirectoryInfoEx;
                        var freshdirs = dir.EnumerateDirectories();
                        var freshfiles = dir.EnumerateFiles();
                        if (freshdirs.Count() + freshfiles.Count() != _children.Count) return false;
                        int i = 0;
                        foreach (var info in freshdirs)
                            if (info.FullName != _children[i++].Info.FullName) return false;
                        foreach (var info in freshfiles)
                            if (info.FullName != _children[i++].Info.FullName) return false;
                    }
                    catch { return false; }
                }
                return true;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;


        internal FSExplorerItem(FSExplorerItem parentDirectory, FileSystemInfoEx info, FSExplorer explorer, int rootId)
        {
            RootId = rootId;

            if (parentDirectory != null && !(parentDirectory.IsDirectory)) throw new ArgumentException("parentDirectory is not a Directory");
            ParentDirectory = parentDirectory;
            _explorer = explorer;

            Info = info;
            _icon = null;
            _name = Info?.Label;
            if (info != null && info.Exists) info.RequestPIDL(pidl => { if (pidl != null) _icon = _iconExtractor.GetBitmapSource(IconSize.small, pidl.Ptr, info.IsFolder, false); return; });

            if (info.Attributes.HasFlag(FileAttributes.Hidden))
            {
                if(!IsLogicalDriveRoot) Visibility = Visibility.Collapsed;
            }
            else Visibility = Visibility.Visible;

            
            ChildrenCount = 0;

            if (IsDirectory && info!= null && info.Exists && IsEnabled)
            {

                var dir = Info as DirectoryInfoEx;

                if (Info.FullName.StartsWith("::")) ChildrenCount =
                        dir.EnumerateDirectories("*", SearchOption.TopDirectoryOnly).Count() +
                        dir.EnumerateFiles("*", SearchOption.TopDirectoryOnly).Count();
                else try
                    {
                        ChildrenCount = Directory.EnumerateDirectories(dir.FullName, "*", SearchOption.TopDirectoryOnly).Count() +
                        Directory.EnumerateFiles(dir.FullName, "*", SearchOption.TopDirectoryOnly).Count();
                    }
                    catch (Exception e)
                    {
                        ChildrenCount = 0;
                        if (e is UnauthorizedAccessException) IsUnauthorizedAccess = true;
                        else
                        {

                        }
                    }
                if (ChildrenCount > 0)
                {
                    Children = new ObservableCollection<FSExplorerItem>() { DummyChild };
                    HasDummyChild = true;
                }
            }
        }
        internal FSExplorerItem(FSExplorerItem copy): this (copy.ParentDirectory, copy.Info, copy.Explorer, copy.RootId) { }
        protected FSExplorerItem() { }

        static internal FSExplorerItem MakeRootItem(List<FSExplorerItem>roots, FSExplorer explorer)
        {
            BitmapSource icon = null;
            var mcd = FSExplorer.MyComputerDirectory;
            mcd.RequestPIDL(pidl => { if (pidl != null) icon = FSExplorerItem._iconExtractor.GetBitmapSource(IconSize.small, pidl.Ptr, true, false); return; } );

            var item = new FSExplorerItem()
            {
                _icon = icon,
                _explorer = explorer,
                ParentDirectory = null,
                Info = new DirectoryInfoEx("::Root"),
                Name = "",
                Visibility = Visibility.Visible,
                Children = new ObservableCollection<FSExplorerItem>()
            };
            foreach (var d in roots) item._children.Add(d);

            return item;
        }

        protected void NotifyPropertyChanged(string PropertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }


        public void Refresh()
        {
            Info.Refresh();
            
            if (IsDirectory && Info!= null && Info.Exists && IsEnabled)
            {
                var dir = Info as DirectoryInfoEx;
                if (Info.FullName.StartsWith("::")) ChildrenCount =
                        dir.EnumerateDirectories("*", SearchOption.TopDirectoryOnly).Count() +
                        dir.EnumerateFiles("*", SearchOption.TopDirectoryOnly).Count();
                else try
                    {
                        ChildrenCount = Directory.EnumerateDirectories(dir.FullName, "*", SearchOption.TopDirectoryOnly).Count() +
                        Directory.EnumerateFiles(dir.FullName, "*", SearchOption.TopDirectoryOnly).Count();
                    }
                    catch { ChildrenCount = 0; }
                RefreshChildren();
            }

            _name = Info?.Label;
            if (Info != null && Info.Exists) Info.RequestPIDL(pidl => { if (pidl != null) _icon = _iconExtractor.GetBitmapSource(IconSize.small, pidl.Ptr, Info.IsFolder, false); return; });
            RefreshProperties();
        }

        public void RefreshProperties()
        {
            Info.Refresh();
            NotifyPropertyChanged("Name");
            NotifyPropertyChanged("Icon");
            NotifyPropertyChanged("Info");
            NotifyPropertyChanged("LastModified");
            NotifyPropertyChanged("TypeName");
            NotifyPropertyChanged("Size");
            NotifyPropertyChanged("IsDirectory");
            NotifyPropertyChanged("IsEnabled");
            NotifyPropertyChanged("IsExpanded");
            NotifyPropertyChanged("ItemType");
            NotifyPropertyChanged("ItemTypeValue");
            NotifyPropertyChanged("Path");
            NotifyPropertyChanged("ParentDirectory");
            NotifyPropertyChanged("Visibility");
        }
        //async Task<ObservableCollection<FSExplorerItem>> FreshChildren()
        //{
        //    if (!(Info is DirectoryInfoEx))
        //    {
        //        return null;
        //    }

        //    var dir = Info as DirectoryInfoEx;

        //    var dirs = await dir.GetDirectoriesAsync(null, SearchOption.TopDirectoryOnly, new System.Threading.CancellationToken()));
        //    var files = await dir.GetFilesAsync(null, SearchOption.TopDirectoryOnly, new System.Threading.CancellationToken());
        //    var freshChildren = new ObservableCollection<FSExplorerItem>();

        //    foreach (var d in dirs)
        //        freshChildren.Add(new FSExplorerItem(this, d, _explorer));

        //    foreach (var f in files)
        //        freshChildren.Add(new FSExplorerItem(this, f, _explorer));

        //    return freshChildren;
        //}

        ObservableCollection<FSExplorerItem> FreshChildren()
        {
            if (!(Info is DirectoryInfoEx))
            {
                return null;
            }

            var dir = Info as DirectoryInfoEx;

            var dirs = dir.EnumerateDirectories();
            var files = dir.EnumerateFiles();
            var freshChildren = new ObservableCollection<FSExplorerItem>();

            foreach (var d in dirs)
                freshChildren.Add(new FSExplorerItem(this, d, _explorer, RootId));

            foreach (var f in files)
                freshChildren.Add(new FSExplorerItem(this, f, _explorer, RootId));

            return freshChildren;
        }

        void RefreshChildren()
        {
            if (ChildrenCount == 0)
            {
                Children = null;
                if (HasDummyChild) HasDummyChild = false;
                
                return;
            }

            if (HasDummyChild || IsFresh) return;

            var freshChildren = FreshChildren();

            if (freshChildren.Count > 0)
            {
                if (_children != null)
                {
                    List<FSExplorerItem> deleted = new List<FSExplorerItem>();
                    foreach (var child in _children)
                    {
                        child.RefreshProperties();
                        if(child.Info.FullName.StartsWith("::") && child.Info.Exists != true) deleted.Add(child);
                        else if ((child.IsDirectory) && !Directory.Exists(child.Info.FullName)) deleted.Add(child);
                        else if (!File.Exists(child.Info.FullName)) deleted.Add(child);
                    }
                    foreach (var child in deleted) _children.Remove(child);
                }
                List<FSExplorerItem> added = new List<FSExplorerItem>();
                List<FSExplorerItem> childrenlist = (_children != null) ? _children.ToList() : null;
                foreach (var c in freshChildren)
                {
                    var child = (childrenlist !=null) ? childrenlist.Find(x => x.Info.FullName == c.Info.FullName) : null;
                    if (child == null)
                    {
                        if (_children == null) Children = new ObservableCollection<FSExplorerItem>();
                        _children.Add(c);
                    }
                }
            }
        }

        public FSExplorerItem ExpandTowardsDirectory(Stack<DirectoryInfoEx> pathstack)
        {
            if (pathstack.Count == 0) return null;
            DirectoryInfoEx childpath = pathstack.Pop();

            IsExpanded = true;
            foreach (var child in _children)
            {
                if (child.Info.FullName == childpath.FullName)
                {
                    if (pathstack.Count == 0) return child;
                    else return child.ExpandTowardsDirectory(pathstack);
                }
            }
            return null;
        }

        public FSExplorerItem FindChild(Stack<DirectoryInfoEx> pathstack)
        {
            if (pathstack.Count == 0) return null;
            if (ChildrenCount == 0 || HasDummyChild) return null;

            DirectoryInfoEx childpath = pathstack.Pop();
            foreach (var child in _children)
            {
                if (child.Info.FullName == childpath.FullName)
                {
                    if (pathstack.Count == 0) return child;
                    else return child.FindChild(pathstack);
                }
            }
            return null;
        }

        private void RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }


        public virtual bool CanDoFileOperations {
            get
            {
                if (Info == null || !Info.Exists) return false;
                if (Info.FullName.StartsWith("::")) return false;
                string path = Info.FullName;
                path = System.IO.Path.GetFullPath(path);
                string root = System.IO.Path.GetPathRoot(path);
                if (path == root) return false;
                return true;
            }
        }

        public virtual bool AllowDrop
        {
            get
            {
                if (!IsDirectory) return false;
                if (Info.FullName.StartsWith("::")) return false;
                if ((Info.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly) return false;
                return true;
            }
        }

        #region Rename, Move, Delete
        //+++++++++++++++++++++++++++
        //RENAME
        private bool _renaming;
        public bool Renaming
        {
            get { return _renaming; }
            set { _renaming = value; NotifyPropertyChanged("Renaming"); }
        }
        StartRenamingFSExplorerItemCommand _startRenamingCommand;
        public StartRenamingFSExplorerItemCommand StartRenamingCommand { get { if (_startRenamingCommand == null) _startRenamingCommand = new StartRenamingFSExplorerItemCommand(this); return _startRenamingCommand; } }
        public void StartRenaming() { Renaming = true; }
        public void Rename()
        {
            if (Info.Label != _name)
            {
                _name = Info?.Label;
                
            }
            Renaming = false;
        }

        public void RenameTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            var keyArgs = e;

            if (keyArgs != null && keyArgs.Key == Key.Enter)
            {
                Rename();
                Renaming = false;
            }
            if (keyArgs != null && keyArgs.Key == Key.Escape)
            {
                Name = Info.Label;
                Renaming = false;
            }
        }
        public void RenameTextBoxLostFocus()
        {
            if (Renaming == true)
            {
                if (Name != Info.Label)
                {
                    Rename();
                    Name = Info.Label;
                }
                Renaming = false;
            }
        }

        //+++++++++++++++++++++++++++++
        //MOVE
        public void MoveTo(string path)
        {

        }
        
        //++++++++++++++++++++++++++++++
        //DELETE
        DeleteFSEXplorerItemCommand _deleteCommand;
        public DeleteFSEXplorerItemCommand DeleteCommand { get { if (_deleteCommand == null) _deleteCommand = new DeleteFSEXplorerItemCommand(this); return _deleteCommand; } }
        public virtual void Delete()
        {

        }
        #endregion

        protected Point _dragStart;
        protected DataObject _dragData;
        public bool IsMouseOverDir { get; set; }

        public void PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e, bool fromTree)
        {
            _dragStart = e.GetPosition(null);
            _dragData = new DataObject("FSExplorerItem", this);
        }
        public void PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e, bool fromTree)
        {
            _dragStart = new Point();
            _dragData = null;
        }
        public void MouseMove(object sender, MouseEventArgs e, bool fromTree)
        {

        }
        public void MouseEnter(object sender, MouseEventArgs e, bool fromTree)
        {
            if (fromTree)
            { 
                IsMouseOverDir = true;
                NotifyPropertyChanged("IsMouseOverDir");
            }
            _dragStart = new Point();
            _dragData = null;
        }
        public void MouseLeave(object sender, MouseEventArgs e, bool fromTree)
        {
            if(fromTree)
            { 
                IsMouseOverDir = false;
                NotifyPropertyChanged("IsMouseOverDir");
            }
            Vector diff = _dragStart - e.GetPosition(null);

            if (e.LeftButton == MouseButtonState.Pressed && _dragData != null &&
                (Math.Abs(diff.X) > 2 || Math.Abs(diff.Y) > 2))
            {
                if (sender is StackPanel tvi && CanDoFileOperations) DragDrop.DoDragDrop(tvi, _dragData, DragDropEffects.Move | DragDropEffects.None);
            }
        }
        public void DragEnter(object sender, DragEventArgs e)
        {
            bool fromTree = (sender is StackPanel sp && sp.Name == "ItemStackPanelTree")? true : false; 

            if (fromTree)
            {
                IsMouseOverDir = true;
                NotifyPropertyChanged("IsMouseOverDir");
            }
        }
        public void DragEnter(object sender, DragEventArgs e, bool fromTree)
        {
            if(fromTree)
            { 
                IsMouseOverDir = true;
                NotifyPropertyChanged("IsMouseOverDir");
            }
        }
        public void DragOver(object sender, DragEventArgs e, bool fromTree)
        {
            bool dvm = e.Data.GetDataPresent("FSExplorerItem");

            if (IsDirectory && !Info.FullName.StartsWith("::") && dvm)
            {
                var sourceDvm = e.Data.GetData("FSExplorerItem") as FSExplorerItem;
                if (sourceDvm == this) e.Effects = DragDropEffects.None;
                else e.Effects = DragDropEffects.Move;
            }
            else e.Effects = DragDropEffects.None;
        }
        public void DragLeave(object sender, DragEventArgs e, bool fromTree)
        {
            if(fromTree)
            { 
                IsMouseOverDir = false;
                NotifyPropertyChanged("IsMouseOverDir");
            }
            _dragStart = new Point();
            _dragData = null;
        }
        public void Drop(object sender, DragEventArgs e, bool fromTree)
        {
            if (IsDirectory && e.Data.GetDataPresent("FSExplorerItem"))
            {
                var sourceDvm = e.Data.GetData("FSExplorerItem") as FSExplorerItem;
                if (sourceDvm == this) return;
                bool selected = sourceDvm.IsSelected;

                string path = Info.FullName + "\\" + sourceDvm.Name;
                sourceDvm.MoveTo(path);
            }
        }

        public void MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is ListViewItem) || IsDirectory == false ) return;

            if(IsNavup)
            {
                if (ParentDirectory != null) _explorer.NavigateTo(ParentDirectory.Info);
                else if (IsRoot) _explorer.NavigateTo(new DirectoryInfoEx("::Root"));
            }
            else _explorer.NavigateTo(Info);
        }

        public BrowseButtonClickedCommand BrowseButtonClickedCmd { get => new BrowseButtonClickedCommand(this); }
        public bool CanBrowseButtonClick { get => true; }
        public void BrowseButtonClicked()
        { IsSelected = true; }


        SortFieldCommand _sortFieldCommand;
        public SortFieldCommand SortFieldCommand { get { if (_sortFieldCommand == null) _sortFieldCommand = new SortFieldCommand(this); return _sortFieldCommand; } }

        //FSExplorerSortField _sortField = FSExplorerSortField.AlphaNumeric;

        public void OrderChildrenBy(FSExplorerSortField field)
        {
            ChildrenCV.SortDescriptions.Clear();
            switch (field)
            {
                case FSExplorerSortField.AlphaNumeric: ChildrenCV.SortDescriptions.Add(new SortDescription("Id", ListSortDirection.Ascending)); break;
                case FSExplorerSortField.CreationTime: ChildrenCV.SortDescriptions.Add(new SortDescription("Info.CreationTime", ListSortDirection.Descending)); break;
                case FSExplorerSortField.LastModified: ChildrenCV.SortDescriptions.Add(new SortDescription("Info.LastModifiedTime", ListSortDirection.Descending)); break;
                case FSExplorerSortField.LastAccessTime: ChildrenCV.SortDescriptions.Add(new SortDescription("Info.LastAccessTime", ListSortDirection.Descending)); break;
            }
            ChildrenCV.Refresh();
        }
    }
    public class SortFieldCommand : ICommand
    {
        internal FSExplorerItem item;
        internal SortFieldCommand(FSExplorerItem item) { this.item = item; }
        public event EventHandler CanExecuteChanged;
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) { item.OrderChildrenBy ( (FSExplorerSortField)parameter); }
        void _CanExecuteChanged() { CanExecuteChanged(this, new EventArgs()); }
    }
    public class StartRenamingFSExplorerItemCommand : ICommand
    {
        internal FSExplorerItem item;
        internal StartRenamingFSExplorerItemCommand(FSExplorerItem item) { this.item = item; }
        public event EventHandler CanExecuteChanged;
        public bool CanExecute(object parameter) { return item.CanDoFileOperations; }
        public void Execute(object parameter) { item.StartRenaming(); }

        //CanExecute never changes since item.CanDoFileOperations is determined at node creation and does not change ever.
        // This line is only here to suppress the warning that CanExecuteChanged, which is mandatory part of ICommand Interface, is never used.
        void _CanExecuteChanged() { CanExecuteChanged(this, new EventArgs()); }
    }

    public class DeleteFSEXplorerItemCommand : ICommand
    {
        public FSExplorerItem item;
        public DeleteFSEXplorerItemCommand(FSExplorerItem item) { this.item = item; }
        public event EventHandler CanExecuteChanged;
        public bool CanExecute(object parameter) { return item.CanDoFileOperations; }
        public void Execute(object parameter) { item.Delete(); }

        //CanExecute never changes since item.CanDoFileOperations is determined at node creation and does not change ever.
        // This line is only here to suppress the warning that CanExecuteChanged, which is mandatory part of ICommand Interface, is never used.
        void _CanExecuteChanged() { CanExecuteChanged(this, new EventArgs()); }
    }

    public class BrowseButtonClickedCommand : ICommand
    {
        public event EventHandler CanExecuteChanged;

        internal FSExplorerItem item;
        internal BrowseButtonClickedCommand(FSExplorerItem item) { this.item = item; }

        public bool CanExecute(object parameter) { return item.CanBrowseButtonClick; }
        public void Execute(object parameter) { item.BrowseButtonClicked(); }

        // CanExecute never changes since item.CanBrowseButtonClick is always true.
        // This line is only here to suppress the warning that CanExecuteChanged, which is mandatory part of ICommand Interface, is never used.
        void _CanExecuteChanged() { CanExecuteChanged(this, new EventArgs()); }
    }

    public class FSExplorerItemComparer : IComparer
    {
        public bool SortDirection { get; set; }
        public string SortField { get; set; }

        public int Compare(object x, object y)
        {
            var X = x as FSExplorerItem;
            var Y = y as FSExplorerItem;
            int result = 0;

            if (x is FSExplorerBookmark && y is FSExplorerBookmark)
            {
                result = X.Id.CompareTo(Y.Id);
                if (result != 0) return (SortDirection) ? result : -result;
            }
            else if (x is FSExplorerBookmark)
            {
                return -1;
            }
            else if (y is FSExplorerBookmark)
            {
                return 1;
            }



            result = X.IsNavup.CompareTo(Y.IsNavup);
            if (result != 0) return -result;

            if (SortField != "Type")
            { 
                result = X.ItemTypeValue.CompareTo(Y.ItemTypeValue);
                if (result != 0) return result;
            }

            switch (SortField)
            {
                case "Id":
                    result = X.Id.CompareTo(Y.Id);
                    if (result != 0) return (SortDirection) ? result : -result;
                    break;
                case "Modified":
                    result = DateTime.Compare(X.Info.LastWriteTime, Y.Info.LastWriteTime);
                    if (result != 0) return (SortDirection) ? result : -result;
                    break;
                case "Type":
                    result = String.Compare(X.TypeName, Y.TypeName);
                    if (result != 0) return (SortDirection) ? result : -result;
                    break;
                case "Creation Time":
                    result = DateTime.Compare(X.Info.CreationTime, Y.Info.CreationTime);
                    if (result != 0) return (SortDirection) ? result : -result;
                    break;
                case "Last Accesss Time":
                    result = DateTime.Compare(X.Info.LastAccessTime, Y.Info.LastAccessTime);
                    if (result != 0) return (SortDirection) ? result : -result;
                    break;
                case "Size":
                    long xL = (X.Info is FileInfoEx finfo) ? finfo.Length : 0;
                    long yL = (Y.Info is FileInfoEx ginfo) ? ginfo.Length : 0;

                    result = xL.CompareTo(yL);
                    if (result != 0) return (SortDirection) ? result : -result;
                    break;
                case "":
                case "Name":
                default:
                    result = X.Id.CompareTo(Y.Id);
                    if (result != 0) return (SortDirection) ? result : -result;
                    break;
            }
            return 0;
        }
    }
}
