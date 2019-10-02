using System;
using System.Collections.Generic;
using System.Linq;
using Caliburn.Micro;
using Bubbles3.Views;
using Bubbles3.Models;
using XamlFSExplorer;
using System.IO;
using System.Collections.ObjectModel;

namespace Bubbles3.ViewModels
{
    public class TabViewModel:Screen
    {
        public TabViewModel(ShellViewModel conductor, BblTabState tabstate, ObservableCollection<TabOptions> savedOptions)
        {
            _conductor = conductor;
            _tabState = tabstate;
            Library = new LibraryViewModel(this);
            DisplayName = tabstate.displayName;

            TabUIState uistate = tabstate.uiState;
            uistate.doLoad = true;
            TabUIState = uistate;
            _savedOptions = savedOptions;

            if (string.IsNullOrEmpty(tabstate.windowedOptions)) tabstate.windowedOptions = "default";
            if (string.IsNullOrEmpty(tabstate.fullscreenOptions)) tabstate.fullscreenOptions = "default";

            foreach (var o in savedOptions)
            {
                if (tabstate.windowedOptions == o.Name) WindowedOptions = o;
                else if (tabstate.fullscreenOptions == o.Name) FullscreenOptions = o;
            }

            TheExplorer = new XamlFSExplorer.FSExplorer(new DirectoryInfoEx[] { FSExplorer.MyComputerDirectory, FSExplorer.LibrariesDirectory })
            {
                HideAttributes = FileAttributes.Hidden | FileAttributes.System | FileAttributes.Offline,
                ShowDisabled = true
            };
            var bookmark = _tabState.currentBookmark;
            bookmark.destroyOnClose = true;
            var fsbm = TheExplorer.AddBookmark(bookmark);
            fsbm.BookmarkClicked += GotoBookmark;
            fsbm.BookmarkDeleted += OnBookmarkDeleted;
            _bookmarks.Add(fsbm);

            foreach (var bm in _tabState.savedBookmarks)
            {
                fsbm = TheExplorer.AddBookmark(bm);
                fsbm.BookmarkClicked += GotoBookmark;
                fsbm.BookmarkDeleted += OnBookmarkDeleted;
                _bookmarks.Add(fsbm);
            }
            _tabState.savedBookmarks.Clear();

            _customSorts.Add(new CustomSort("Natural"));
            _customSorts.Add(new CustomSort("Path"));
            _customSorts.Add(new CustomSort("Name"));
            _customSorts.Add(new CustomSort("Creation Time"));
            _customSorts.Add(new CustomSort("Last Modified"));
            _customSorts.Add(new CustomSort("Randomized"));

            if (string.IsNullOrEmpty(_tabState.booksSort))
            {
                _tabState.booksSort = "Creation Time";
            }

            SelectedCustomSort = _customSorts.Where(x => x.Name == _tabState.booksSort).FirstOrDefault();
            SortDirection = _tabState.booksSortDirection;
        }

        FSExplorer _theExplorer = null;
        public FSExplorer TheExplorer
        {
            get => _theExplorer;
            set
            {
                _theExplorer = value;
                NotifyOfPropertyChange(() => TheExplorer);
            }
        }

        FileSystemInfoEx _selectedFolder;
        public FileSystemInfoEx SelectedFolder
        {
            get => _selectedFolder;
            set
            {
                if(value == null)
                {
                    return;
                }
                if(_selectedFolder == null || value.FullName != _selectedFolder.FullName)
                { 
                    _selectedFolder = value;
                    DisplayName = value.Name;
                    _tabState.displayName = value.Name;
                    if(value is DirectoryInfoEx) _library.Open(value as DirectoryInfoEx);
                    NotifyOfPropertyChange(() => DisplayName);
                    NotifyOfPropertyChange(() => SelectedFolder);
                }
            }
        }

        private LibraryViewModel _library;
        public LibraryViewModel Library
        {
            get => _library; 
            set {
                _library = value;
                NotifyOfPropertyChange(() => Library);
            }
        }

        BblTabState _tabState;
        public BblTabState TabState { get { return _tabState; } }
        ShellViewModel _conductor;
        public ShellViewModel Conductor { get { return _conductor;  } }

        bool _pageVisible = true;
        public bool PageVisible
        {
            get { return _pageVisible; }
            set { _pageVisible = value; NotifyOfPropertyChange(() => PageVisible); }
        }

        bool _explorerVisible = false;
        public bool ExplorerVisible
        {
            get { return _explorerVisible; }
            set { _explorerVisible = value; NotifyOfPropertyChange(() => ExplorerVisible); }
        }

        private bool _bookVisible;
        public bool BookVisible
        {
            get { return _bookVisible; }
            set { _bookVisible = value; NotifyOfPropertyChange(() => BookVisible); }
        }

        private TabUIState _tabUIState;
        public TabUIState TabUIState
        {
            get { return _tabUIState; }
            set { _tabUIState = value; NotifyOfPropertyChange(() => TabUIState); }
        }

        public int BooksCount => _library.Books.Count();

        string _pagePath;
        public string PagePath {
            get => _pagePath;
            set
            {
                _pagePath = value;
                NotifyOfPropertyChange(() => PagePath);
            }
        }

        string _pageSize;
        public string PageSize
        {
            get => _pageSize;
            set
            {
                _pageSize = value;
                NotifyOfPropertyChange(() => PageSize);
            }
        }


        protected override void OnActivate()
        {
            base.OnActivate();
            if (!string.IsNullOrEmpty(TabState.navigated))
            {
                if (SelectedFolder != null && SelectedFolder.FullName == TabState.navigated) _library.OnTabReactivated();
                else SelectedFolder = new DirectoryInfoEx(_tabState.navigated);
            }
            _tabState.isActive = true;
        }

        
        protected override void OnDeactivate(bool close)
        {
            _tabState.isActive = false;
            _library.OnTabDeactivated(close);
            if (close)
            {
                foreach(var fsbm in Bookmarks)
                {
                    fsbm.BookmarkClicked -= GotoBookmark;
                    fsbm.BookmarkDeleted -= OnBookmarkDeleted;
                }
                Bookmarks.Clear();

                _theExplorer.Dispose();
            }
            base.OnDeactivate(close);
        }

        string _filterText;
        public String FilterText
        {
            get { return _filterText; }
            set
            {
                if (_filterText != value)
                {
                    _filterText = value;
                    NotifyOfPropertyChange(() => FilterText);
                }
            }
        }

        public void StartSearch()
        {
            FilterView();

        }

        public bool ShowClearFilterButton
        { get { return (FilterText != null && FilterText != ""); } }

        public void ClearFilter()
        {
            FilterText = "";
            FilterView();
        }


        public void FilterView()
        {
            NotifyOfPropertyChange(() => ShowClearFilterButton);
            Library.Filter(FilterText);
        }

        public void ExecuteFilterView(ActionExecutionContext context)
        {
            var keyArgs = context.EventArgs as System.Windows.Input.KeyEventArgs;

            if (keyArgs != null && keyArgs.Key == System.Windows.Input.Key.Enter)
            {
                FilterView();
            }
        }

        List<FSExplorerBookmark> _bookmarks = new List<FSExplorerBookmark>();
        public List<FSExplorerBookmark> Bookmarks => _bookmarks;
        public void GotoBookmark(object sender, EventArgs e)
        {
            var fsbm = sender as FSExplorerBookmark;
            var bm = (BblBookmark)fsbm.UserData;
            Library.GotoBookmark(bm);

        }
        public void OnBookmarkDeleted(object sender, EventArgs e)
        {
            var fsbm = sender as FSExplorerBookmark;
            fsbm.BookmarkDeleted -= OnBookmarkDeleted;
            fsbm.BookmarkClicked -= GotoBookmark;
            _bookmarks.Remove(fsbm);
        }
        public void AddBookmark()
        {
            var bm = new BblBookmark()
            {
                libraryPath = Library.Model.Root.Path,
                bookPath = Library.SelectedBook.Model.Path,
                pageFilename = Library.SelectedBook.SelectedPage.Model.Filename
            };
            var fsbm =TheExplorer.AddBookmark(bm);
            fsbm.BookmarkClicked += GotoBookmark;
            fsbm.BookmarkDeleted += OnBookmarkDeleted;
            _bookmarks.Add(fsbm);
        }

        public bool CanAddBookmark
        {
            get => Library?.SelectedBook?.SelectedPage != null;
        }
        public void OnPageSelected(PageViewModel page)
        {
            TabState.currentBookmark.pageFilename = (page != null)?page.Model.Filename:null;
            NotifyOfPropertyChange(() => CanAddBookmark);
        }
        public void OnBookSelected(BookViewModel book)
        {
            TabState.currentBookmark.bookPath = (book != null) ? book.Path : null;
        }

        private ObservableCollection<CustomSort> _customSorts = new ObservableCollection<CustomSort>();
        public ObservableCollection<CustomSort> CustomSorts
        {
            get { return _customSorts; }
            set
            {
                _customSorts = value;
                NotifyOfPropertyChange(() => CustomSorts);
            }
        }

        public CustomSort SelectedCustomSort
        {
            get { return Library.comparer.sortBy; }
            set
            {
                Library.comparer.sortBy = value;
                NotifyOfPropertyChange(() => SelectedCustomSort);
                Library.SortBooks();
                _tabState.booksSort = value.Name;
            }
        }

        public bool SortDirection
        {
            get { return Library.comparer.sortDirection; }
            set
            {
                Library.comparer.sortDirection = value;
                NotifyOfPropertyChange(() => SortDirection);
                Library.SortBooks();
                _tabState.booksSortDirection = value;
            }
        }

        internal void ResetSortUI()
        {
            if (SelectedCustomSort == null)
            {
                SelectedCustomSort = _customSorts[5]; //Last modified
                SortDirection = false;
            }
        }

        public void RefreshSort()
        {
            Library.SortBooks();
        }

        public bool SaveToDBToggle
        {
            get { return TabState.saveToDB; }
            set
            {
                TabState.saveToDB = value;
                NotifyOfPropertyChange(() => SaveToDBToggle);
            }
        }
        public void ScrollToSelected()
        {
            Library.ScrollToSelected();
        }

        ObservableCollection<TabOptions> _savedOptions;
        public ObservableCollection<TabOptions> SavedOptions => _savedOptions;

        TabOptions _windowedOptions = new TabOptions();
        public TabOptions WindowedOptions
        {
            get { foreach (var p in SavedOptions) if (p.Equals(_windowedOptions)) return p; return null; }
            set {
                _windowedOptions = value;

                _tabState.windowedOptions = value.Name;
                NotifyOfPropertyChange(() => WindowedOptions);
            }
        }

        TabOptions _fullscreenOptions = new TabOptions();
        public TabOptions FullscreenOptions
        {
            get { foreach (var p in SavedOptions) if (p.Equals(_fullscreenOptions)) return p; return null; }
            set {
                _fullscreenOptions = value;

                _tabState.fullscreenOptions = value.Name;
                NotifyOfPropertyChange(() => FullscreenOptions);
            }
        }

        public void ManageSettings()
        {
            IWindowManager manager = new WindowManager();
            manager.ShowDialog(new TabOptionsViewModel(_windowedOptions, _savedOptions), null, null);
            NotifyOfPropertyChange(() => SavedOptions);
            NotifyOfPropertyChange(() => FullscreenOptions);
            NotifyOfPropertyChange(() => WindowedOptions);
        }

        public void OnRequestPageDeletion(BblPage page)
        {
            //if (Library?.SelectedBook?.SelectedPage != null && Library.SelectedBook.SelectedPage.Model == page) Library.SelectedBook.DeleteSelectedPage();
        }
        public void OnRequestNextPage()
        {
            if (Library?.SelectedBook == null) return;
            if (Library.SelectedBook.SelectNextPage() == false)
                Library.SelectNextBook();

        }
        public void OnRequestPreviousPage()
        {
            if (Library?.SelectedBook == null) return;
            if (Library.SelectedBook.SelectPreviousPage() == false)
                Library.SelectPreviousBook();
        }
        public void OnRequestFirstPage()
        {
            if (Library?.SelectedBook == null) return;
            Library.SelectedBook.SelectFirstPage();
        }
        public void OnRequestLastPage()
        {
            if (Library?.SelectedBook == null) return;
            Library.SelectedBook.SelectLastPage();
        }
        public void OnRequestNextBook()
        {
            if(Library != null) Library.SelectNextBook();
        }
        public void OnRequestPrevBook()
        {
            if (Library != null) Library.SelectPreviousBook();
        }

        int _progressValue = 0;
        public int ProgressValue
        {
            get => _progressValue;
            set { _progressValue = value; NotifyOfPropertyChange(() => ProgressValue); }
        }

        int _progressMaximum = 100;
        public int ProgressMaximum
        {
            get => _progressMaximum;
            set { _progressMaximum = value; NotifyOfPropertyChange(() => ProgressMaximum); }
        }

        bool _progressIsDeterminate = true;
        public bool ProgressIsDeterminate
        {
            get => _progressIsDeterminate;
            set { _progressIsDeterminate = value; NotifyOfPropertyChange(() => ProgressIsDeterminate); }
        }
    }
}
