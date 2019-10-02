using ShellDll;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using XamlFSExplorer.Utils;

namespace XamlFSExplorer
{
    public delegate void FSExplorerEventHandler(FSExplorer sender, FSExplorerEventArgs e);

    public class FSExplorer : INotifyPropertyChanged, IDisposable
    {
        public static DirectoryInfoEx MyComputerDirectory = new DirectoryInfoEx(KnownFolderIds.ComputerFolder);
        public static DirectoryInfoEx LibrariesDirectory = new DirectoryInfoEx(KnownFolderIds.Libraries);
        public static DirectoryInfoEx NetworkFolder = new DirectoryInfoEx(KnownFolderIds.NetworkFolder);

        Dictionary<string, FileSystemWatcher> fsDirectoriesWatchers = new Dictionary<string, FileSystemWatcher>();

        /// <summary>
        /// Hide Files & folders with attribute
        /// Use Offline to hide ejected drives.
        /// </summary>
        public FileAttributes HideAttributes { get; set; }
        
        /// <summary>
        /// Disabled files & folders (ejected drives, unauthorized access...)
        /// will be shown but greyed out if this is set to true.
        /// </summary>
        public bool ShowDisabled { get; set; }

        /// <summary>
        /// only matching files will be included (case insensitive)
        /// wildcards are
        /// , : separator
        /// ? : zero or one character
        /// * : zero or more characters
        /// example : "*.txt", "*jpg", "*_64.???"
        /// </summary>
        public string IncludeFilesFilter { get; set; }


        //Root Items
        ObservableCollection<FSExplorerItem> _rootItems = new ObservableCollection<FSExplorerItem>();
        public ObservableCollection<FSExplorerItem> RootItems
        {
            get => _rootItems;
            set { _rootItems = value; NotifyPropertyChanged("RootItems"); }
        }
        public FSExplorerItem RootItem { get; set; }

        //Current Folder
        FSExplorerItem _currentFolderItem;
        internal FSExplorerItem CurrentFolderItem
        {
            get { return _currentFolderItem; }
            set
            {
                if(_currentFolderItem != value)
                {
                    if(!value.IsDirectory)
                        throw new InvalidDataException("FSExplorer.CurrentFolderItem value.Info must be of the type DirectoryInfoEx");

                    _currentFolderItem = value;


                    CurrentPath.Clear();
                    var item = CurrentFolderItem;
                    do
                    {
                        CurrentPath.Insert(0, item);
                        item = item.ParentDirectory;
                    }
                    while (item != null) ;
                    
                    NotifyPropertyChanged("CurrentFolderItem");
                    NotifyPropertyChanged("CurrentFolder");
                    NotifyPropertyChanged("ListItemsCV");
                    SelectedTreeItem = value;
                }
            }
        }

        public DirectoryInfoEx CurrentFolder
        {
            get => CurrentFolderItem?.Info as DirectoryInfoEx;
            set => NavigateTo(value);
        }

        ObservableCollection<FSExplorerItem> _currentPath = new ObservableCollection<FSExplorerItem>();
        public ObservableCollection<FSExplorerItem> CurrentPath { get => _currentPath; }

        //Tree ItemsSource

        FSExplorerItem _selectedTreeItem;
        internal FSExplorerItem SelectedTreeItem
        {
            get => _selectedTreeItem;
            set
            {
                var sel = SelectedTreeItem;
                var from = _selected;
                var fromItem = (_selected == _selectedTreeItem?.Info) ? _selectedTreeItem : _selectedListItem;

                FSExplorerItem seldir = (value.IsDirectory) ? value : value.ParentDirectory ;
                if (seldir != CurrentFolderItem) NavigateTo(seldir.Info, value.RootId);

                if (_selectedTreeItem != value)
                {
                    _selectedTreeItem = value;
                    _selected = value.Info;
                    
                    NotifyPropertyChanged("SelectedTreeItem");
                    NotifyPropertyChanged("Selected");
                    SelectionChanged?.Invoke(this, new FSExplorerEventArgs() { From = from, To = value.Info, FromItem = fromItem, ToItem = value});
                }
                if (seldir != CurrentFolderItem)
                {
                    Navigated?.Invoke(this, new FSExplorerEventArgs() { From = from, To = seldir.Info, FromItem=fromItem, ToItem = seldir });

                }
                //var test = ListItems;
            }
        }
        FileSystemInfoEx _selected;
        public FileSystemInfoEx Selected
        {
            get => _selected;
            set { NavigateTo(value); }
        }




        FSExplorerItemComparer _comparer = new FSExplorerItemComparer();
        internal FSExplorerItemComparer Comparer { get => _comparer; set => _comparer = value; }

        //List ItemsSource
        internal bool ShowListParentFolder { get; set; }
        
        ObservableCollection<FSExplorerItem> _listItems;
        internal ObservableCollection<FSExplorerItem> ListItems {  get => _listItems; }
        ListCollectionView _listItemsCV;
        public ListCollectionView ListItemsCV
        {
            get
            {
                if (_listItems == null)
                {
                    _listItems = new ObservableCollection<FSExplorerItem>();
                    _listItemsCV = (ListCollectionView)CollectionViewSource.GetDefaultView(_listItems);
                    _listItemsCV.CustomSort = _comparer;
                    
                }

                if (CurrentFolderItem.HasDummyChild) CurrentFolderItem.CreateChildren();
                _listItems.Clear();


                

                if (ShowListParentFolder && CurrentFolderItem.IsRoot == false)
                {
                    FSExplorerItem navup = ( CurrentFolderItem?.ParentDirectory != null)? 
                        new FSExplorerItem(CurrentFolderItem) { IsNavup = true } : new FSExplorerItem(RootItem) { IsNavup = true };
                    _listItems.Add(navup);
                }
                if (CurrentFolderItem.Children != null)
                    foreach (var c in CurrentFolderItem.Children) _listItems.Add(c);


                return _listItemsCV;
            }
        }

        FSExplorerItem _selectedListItem;
        public FSExplorerItem SelectedListItem
        {
            get => _selectedListItem;
            set
            {
                if (value == _selectedListItem) return;
                if (value != null)
                {
                    //NavigateTo(value.Info);
                }
                var from = _selected;
                var fromItem = (_selectedListItem?.Info == _selected) ? _selectedListItem : _selectedTreeItem;
                _selectedListItem = value;
                _selected = value.Info;
                NotifyPropertyChanged("Selected");
                SelectionChanged?.Invoke(this, new FSExplorerEventArgs() { From = from, To = value.Info, FromItem = fromItem, ToItem = value });
            }
        }

        FSExplorerBookmarkRoot _bookmarksRoot;

        internal ObservableCollection<FileSystemInfoEx> CheckedItems { get; set; }

        private SynchronizationContext _syncContext;

        public event PropertyChangedEventHandler PropertyChanged;

        public FSExplorer(DirectoryInfoEx[] roots)
        {
            _syncContext = SynchronizationContext.Current;

            int i = 0;
            for (; i < roots.Length; i++)
            {
                var d = roots[i];
                if (d.Exists) _rootItems.Add(new FSExplorerItem(null, d, this, i));
            }

            _bookmarksRoot = new FSExplorerBookmarkRoot(this, i);
            _rootItems.Add(_bookmarksRoot) ;

            if (fsDirectoriesWatchers.Count() == 0)
            {
                LogicalDisk.LogicalDiskArrayModified += LogicalDisksArrayModified;
                var drives = LogicalDisk.Disks;//DriveInfo.GetDrives();

                foreach (var d in drives.Values)
                {
                    try
                    {
                        var fsw = new FileSystemWatcher(d.Name + "\\");
                        if (fsw != null)
                        {
                            fsw.IncludeSubdirectories = true;
                            fsw.EnableRaisingEvents = true;
                            fsDirectoriesWatchers.Add(d.Name,fsw);
                        }
                    }
                    catch { }
                }
            }

            foreach (var fsw in fsDirectoriesWatchers.Values)
            {
                if (fsw == null) continue;
                fsw.Created += FileSystemChanged;
                fsw.Deleted += FileSystemChanged;
                fsw.Renamed += FileSystemChanged;
                fsw.Changed += FileSystemChanged;
            }

            RootItem = FSExplorerItem.MakeRootItem(RootItems.ToList(), this);
            NavigateTo(new DirectoryInfoEx("::Root"));
            
        }

        internal void NotifyPropertyChanged(string PropertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }

        public void NavigateTo(FileSystemInfoEx path, int rootId=0)
        {
            DirectoryInfoEx dir = null;
            dir = (path is DirectoryInfoEx) ? path as DirectoryInfoEx : (path as FileInfoEx)?.Directory;
            var from = CurrentFolderItem;
            //Reset custom sort
            _comparer.SortField = "Name";
            _comparer.SortDirection = true;
            if (dir == null || dir.Exists != true)
            {
                if(dir.FullName=="::Root")
                {
                    CurrentFolderItem = RootItem;
                    CurrentFolderItem.IsSelected = true;
                }
                else if (SelectedTreeItem != null)
                {
                    SelectedTreeItem.IsSelected = false;
                }
            }
            else
            {
                int idx = BuildPathStack(dir, out Stack<DirectoryInfoEx> pathstack, rootId);
                if (idx == -1) return;

                FSExplorerItem targetdir = null;

                if (pathstack.Count == 0) targetdir = RootItems[idx];
                else targetdir = RootItems[idx].ExpandTowardsDirectory(pathstack);

                if (targetdir != null)
                {
                    if (from != null) from.IsSelected = false;
                    CurrentFolderItem = targetdir;
                    
                    if (targetdir.Info.FullName == path.FullName)
                    {
                        
                        targetdir.IsSelected = true;
                     
                    }
                    else
                    {
                        targetdir.IsExpanded = true;
                        foreach (var c in targetdir.Children)
                            if (c.Info.FullName == path.FullName)
                            {
                                from.IsSelected = false;
                                c.IsSelected = true;
                            }
                    }
                    
                    Navigated?.Invoke(this, new FSExplorerEventArgs() { To = targetdir.Info, From = from.Info, ToItem = targetdir, FromItem = from });
                }
            }
            //NotifyPropertyChanged("CurrentFolderItem");
            //NotifyPropertyChanged("ListItemsCV");
            //return null;
        }

        public List<FSExplorerBookmark> Bookmarks => _bookmarksRoot.Children.Cast<FSExplorerBookmark>().ToList();
        public FSExplorerBookmark AddBookmark(Object userData)
        {
            return _bookmarksRoot.AddBookmark(userData);
        }

        public void BookmarkSelected()
        {
            _selectedTreeItem.IsSelected = true;
        }
        /// <summary>
        /// make a stack of directoryinfoex: representing a path for the purpose of searching from root to path
        /// example : path is "c:\windows\System32"
        /// "c:"
        /// "c:\windows"
        /// "c:\windows\System32"
        /// </summary>
        /// <param name="path">the full path</param>
        /// <param name="pathstack">the output</param>
        /// <returns>index of this path's root in the _rootItems array, -1 if this path is not rooted in any of the RootItems</returns>
        int BuildPathStack(DirectoryInfoEx path, out Stack<DirectoryInfoEx> pathstack, int rootId)
        {
            pathstack = new Stack<DirectoryInfoEx>();

            if (rootId == -1) rootId = (SelectedTreeItem != null)?SelectedTreeItem.RootId: 0;
            //for (int i = 0; i < _rootItems.Count; i++)
            //    if (path.FullName == _rootItems[i].Info.FullName) return i;
            if (path.FullName == _rootItems[rootId].Info.FullName) return rootId;

            while (path != null)
            {
                pathstack.Push(path);
                try { path = path.Parent; }
                catch { pathstack = null; return -1; }

                if (path == null) return -1;

                //for (int i = 0; i < _rootItems.Count; i++)
                //    if (path.FullName == _rootItems[i].Info.FullName) return i;
                if (path.FullName == _rootItems[rootId].Info.FullName) return rootId;
            }
            return -1;
        }

        public event FSExplorerEventHandler Navigated;
        public event FSExplorerEventHandler SelectionChanged;


        internal void LogicalDisksArrayModified(LogicalDisk.DriveEventType type, string driveName)
        {
            if (driveName == "C:") return;
            _syncContext.Post(o => RefreshDrives(type, driveName), null);
        }

        internal void RefreshDrives(LogicalDisk.DriveEventType type, string driveName)
        {
            if((type == LogicalDisk.DriveEventType.Created)
                && fsDirectoriesWatchers.ContainsKey(driveName) != true)
            {
                try
                {
                    var fswc = new FileSystemWatcher(driveName + "\\");
                    if (fswc != null)
                    {
                        fswc.IncludeSubdirectories = true;
                        fswc.EnableRaisingEvents = true;
                        fswc.Created += FileSystemChanged;
                        fswc.Deleted += FileSystemChanged;
                        fswc.Renamed += FileSystemChanged;
                        fswc.Changed += FileSystemChanged;
                        fsDirectoriesWatchers.Add(driveName, fswc);
                        Refresh(new FileSystemEventArgs(WatcherChangeTypes.Created, driveName+"\\", null));
                    }
}
                catch { }
            }
            else if ((type == LogicalDisk.DriveEventType.Deleted ) && 
                fsDirectoriesWatchers.TryGetValue(driveName, out FileSystemWatcher fswd))
            {
                    fswd.Changed -= FileSystemChanged;
                    fswd.Created -= FileSystemChanged;
                    fswd.Deleted -= FileSystemChanged;
                    fswd.Renamed -= FileSystemChanged;
                    Refresh(new FileSystemEventArgs(WatcherChangeTypes.Changed, "::PC", null));
                    fsDirectoriesWatchers.Remove(driveName);
                    
            }
            else if( type == LogicalDisk.DriveEventType.Inserted || type == LogicalDisk.DriveEventType.Ejected)
            {
                Refresh(new FileSystemEventArgs(WatcherChangeTypes.Changed, driveName, null));
            }
        }

        
        internal void FileSystemChanged(object sender, FileSystemEventArgs e)
        {
            Task.Run(() => FileSystemChangedAsync(sender, e));
        }

        static string[] _watchBlackList = new string[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.Machine),
            Environment.GetEnvironmentVariable("TMP", EnvironmentVariableTarget.Machine),
            Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.User),
            Environment.GetEnvironmentVariable("TMP", EnvironmentVariableTarget.User),
            Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.Process),
            Environment.GetEnvironmentVariable("TMP", EnvironmentVariableTarget.Process),
        };
        void FileSystemChangedAsync(object sender, FileSystemEventArgs e)
        {
            foreach (string s in _watchBlackList) if (e.FullPath.StartsWith(s)) return;
            _syncContext.Post(o => Refresh(e), null);
        }
        internal void Refresh(FileSystemEventArgs e)
        {
            string path = e.FullPath;
            
            DirectoryInfoEx refresh = null;

            try {
                if (path == "::PC\\") refresh = MyComputerDirectory;
                else refresh = new DirectoryInfoEx(path);
                if(refresh.Exists && e.ChangeType!= WatcherChangeTypes.Changed) refresh = refresh.Parent;
                if ( refresh == null || !refresh.Exists ) return;
            }
            catch { return; }

            int idx = BuildPathStack(refresh, out Stack<DirectoryInfoEx> changePathStack, -1);
            if (idx > - 1)
            {
                FSExplorerItem changed = null;
                if (changePathStack == null || changePathStack.Count == 0) changed = RootItems[idx];
                else changed = _rootItems[idx].FindChild(changePathStack);
                
                if (changed != null)
                { 
                    changed.Refresh();
                    if (changed == CurrentFolderItem || changed ==  CurrentFolderItem.ParentDirectory)
                    {
                        NotifyPropertyChanged("CurrentFolderItem");
                        NotifyPropertyChanged("CurrentFolder");
                        NotifyPropertyChanged("ListItemsCV");
                    }
                }
            }

            if (CurrentFolderItem != null && CurrentFolderItem.IsEnabled == false)
            {
                var parent = CurrentFolderItem.ParentDirectory;
                var navto = (parent != null) ? parent.Info : new DirectoryInfoEx("::Root");
                    NavigateTo(navto);
            }
        }

        internal void OnTVPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (SelectedTreeItem == null || SelectedTreeItem.Renaming) return;

            if (e.Key == Key.F2 && SelectedTreeItem.CanDoFileOperations) SelectedTreeItem.StartRenaming();
            else if (e.Key == Key.Delete && SelectedTreeItem.CanDoFileOperations) SelectedTreeItem.Delete();
        }

        public void Dispose()
        {
            foreach (var fsw in fsDirectoriesWatchers.Values)
                fsw.Dispose();
        }
    }

    public class FSExplorerEventArgs
    {
        public enum FSExplorerEventTypes { Navigated, SelectionChanged}
        public FSExplorerEventTypes Type { get; set; }
        public FileSystemInfoEx To { get; set; }
        public FileSystemInfoEx From { get; set; }
        internal FSExplorerItem ToItem { get; set; }
        internal FSExplorerItem FromItem { get; set; }
    }

    public enum FSExplorerSortField
    {
        AlphaNumeric,
        CreationTime,
        LastModified,
        LastAccessTime,
    }
    public enum FSExplorerSortDirection
    {
        Ascending,
        Descending
    }
}
