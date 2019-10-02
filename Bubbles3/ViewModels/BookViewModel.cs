using Bubbles3.Utils;
using Bubbles3.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Caliburn.Micro;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows.Input;
using System.Windows;
using Bubbles3.Controls;
using System.ComponentModel;
using System.Windows.Data;
using System.Collections.ObjectModel;
using System.Collections;
using System.Threading;
using System.Collections.Specialized;
using System.Windows.Threading;

namespace Bubbles3.ViewModels
{
    public class BookViewModel : PropertyChangedBase, IViewModel<BblBook>, IViewModelProvider<PageViewModel, BblPage>, IVirtualizableItem
    {
        private static ObservableCollection<PageViewModel> _emptyPages = new ObservableCollection<PageViewModel>();
        public static ObservableCollection<PageViewModel> _emptypagescollection = new ObservableCollection<PageViewModel>();

        BblBook _book;
        VmCollection<PageViewModel, BblPage> _pages;
        ListCollectionView _pagesCV = (ListCollectionView)CollectionViewSource.GetDefaultView(_emptyPages);
        public LibraryViewModel Library { get; set; }

        public BookViewModel(BblBook book, LibraryViewModel library)
        {
            _book = book;
            Library = library;
            book.Demoted += OnBookDemoted;
            book.Deleted += OnBookDeleted;
            book.Renamed += OnBookRenamed;
            book.Populated += OnBookPopulated;
            book.Unpopulated += OnBookUnpopulated;
            book.ThumbnailLoaded += OnBookThumbnailChanged;

        }



        public BblBook Model { get => _book; }
        public BblBook Book => Model;
        public int Index => _book.Index;
        public string Path => _book.Path;
        public DateTime CreationTime => _book.CreationTime;
        public DateTime LastWriteTime => _book.LastWriteTime;

        private string _name;
        public String Name
        {
            get => (string.IsNullOrEmpty(_name)) ? _book.Name : _name;
            set { _name = value; NotifyOfPropertyChange(() => Name); }
        }

        public String PageCount => (_book.PageCount > 0) ? _book.PageCount + " pages." : "";

        bool _thumbRequested;
        public BitmapSource Thumbnail
        {
            get
            {
                _thumbRequested = true;
                return _book.Thumbnail;
            }
        }

        public String TooltipText
        {
            get
            {
                return Book.Name + "\n" +
                    Book.Path + "\n" +
                    "Creation Time : " + Book.CreationTime + "\n" +
                    "Last Modified : " + Book.LastWriteTime;
            }
        }


        public ObservableCollection<PageViewModel> Pages => (_book != null) ? _pages : _emptypagescollection;
        public ListCollectionView PagesCV => _pagesCV;
        public ObservableCollection<BblTag> Tags { get; set; }

        int _random = -1;
        public int RandomInt { get { if (_random == -1) RefreshRandomInt(); return _random; } }
        public void RefreshRandomInt() { _random = ThreadSafeRandom.ThisThreadsRandom.Next(); }

        public bool IsViewModelOf(BblBook model)
        {
            return (_book.Equals(model));
        }

        private double _scrollToIndex;
        public double ScrollToIndex
        {
            get { return _scrollToIndex; }
            set { _scrollToIndex = value; NotifyOfPropertyChange(() => ScrollToIndex); }
        }
        public void ScrollToSelected()
        {
            if (PagesCV != null)
                ScrollToIndex = PagesCV.CurrentPosition;
        }
        public void ScrollToPage(PageViewModel p)
        {
            if (PagesCV != null)
                ScrollToIndex = _pagesCV.IndexOf(p);
        }
        int _bookProgress;
        public int BookProgress
        {
            get => _bookProgress;
            set
            {
                _bookProgress = value;
                NotifyOfPropertyChange(() => BookProgress);
            }
        }
        int _bookProgressMax = 100;
        public int BookProgressMax
        {
            get => _bookProgressMax;
            set
            {
                _bookProgressMax = value;
                NotifyOfPropertyChange(() => BookProgressMax);
            }
        }

        bool _showIvp;
        public bool ShowIvp
        {
            get => _showIvp;
            set {
                Library.Tab.TabState.showIvp = value;
                _showIvp = value;
                NotifyOfPropertyChange(() => ShowIvp);
                foreach (var p in _pages) p.OnShowIvpToggled();
            }
        }

        PageViewModel _selectedPage;
        public PageViewModel SelectedPage
        {
            get { return _selectedPage; }
            set
            {
                if(value != _selectedPage)
                {
                    _selectedPage = value;
                    
                    if (IsSelected && ShellViewModel.Instance != null) ShellViewModel.Instance.ShowPage(_selectedPage);
                    NotifyOfPropertyChange(() => SelectedPage);
                    Library.Tab.OnPageSelected(value);

                    //preload next / prev row of pages if it is not realized by bringing it to view => Images Loaded in idle time
                    //shortening the wait when RequestNextPage / RequestPreviousPage are called.
                    if (value != null)
                    {
                        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new System.Action(() =>
                        {
                            if (_pagesCV != null && _pagesCV.CurrentPosition < _pagesCV.Count - 1)
                            {
                                int nextId = _pagesCV.CurrentPosition + 1;
                                var next = (PageViewModel)_pagesCV.GetItemAt(nextId);
                                if (!next.IsRealized) ScrollToIndex = nextId;
                                else if(_pagesCV.CurrentPosition > 0)
                                {
                                    int prevId = _pagesCV.CurrentPosition - 1;
                                    var prev = (PageViewModel)_pagesCV.GetItemAt(prevId);
                                    if (!prev.IsRealized) ScrollToIndex = prevId;
                                }
                            }
                        }));
                    }
                }
            }
        }

        IEnumerable _selectedPages;
        public IEnumerable SelectedPages
        {
            get => _selectedPages;
            set { _selectedPages = value; }
        }
        private bool _isSelected = false;
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    NotifyOfPropertyChange(() => IsSelected);
                    if (value == true)
                    {
                        if (!IsRealized) Library.ScrollToBook(this);

                        if (!_populated) 
                            Model.PopulateAsync(IsSelected ? 1 : 2);
                        else if (!_thumbRequested && !Model.IsThumbnailLoaded)
                            Model.LoadThumbnail(IsSelected?1:2);
                        else Open();
                    }
                    else if (value == false)
                    {
                        Close();
                        if (!IsRealized) Unpopulate();
                    }
                }
            }
        }


        private bool _isRealized;
        public bool IsRealized
        {
            get { return _isRealized; }
            set
            {
                if (_isRealized != value)
                {
                    _isRealized = value;
                    NotifyOfPropertyChange(() => IsRealized);

                    if (_isRealized)
                    {
                        if (!(Model.IsPopulated || Model.IsPopulating)) Model.PopulateAsync(IsSelected ? 1 : 2);
                        else if (!(Model.IsThumbnailLoaded || Model.IsThumbnailLoading)) Model.LoadThumbnail(IsSelected ? 1 : 2);
                    }
                    else
                    {
                        if (!_isSelected) Unpopulate();
                    }
                }
            }
        }

        public bool IsPopulated => _populated;

        private void OnBookThumbnailChanged(object sender, EventArgs e)
        {
            if (IsSelected && !_populated) Populate();
            if (IsSelected && !_open) Open();
            NotifyOfPropertyChange(() => Book.Thumbnail);
        }

        private void OnBookPopulated(object sender, EventArgs e)
        {
            NotifyOfPropertyChange(() => Book.PageCount);
            NotifyOfPropertyChange(() => PageCount);
            NotifyOfPropertyChange(() => Book.Pages);

            if (!_populated)
                Populate(); 
            else NotifyOfPropertyChange(() => PagesCV);

        }

        private void OnBookUnpopulated(object sender, EventArgs e)
        {
            if (_populated) Unpopulate();
        }

        private void OnBookRenamed(object sender, EventArgs e)
        {
            NotifyOfPropertyChange(() => Book.Index);
            NotifyOfPropertyChange(() => Index);
            NotifyOfPropertyChange(() => Book.Name);
            Name = Book.Name;
            NotifyOfPropertyChange(() => Book.Path);
            NotifyOfPropertyChange(() => Book.CreationTime);
            NotifyOfPropertyChange(() => Book.LastWriteTime);
            NotifyOfPropertyChange(() => TooltipText);
            if (_open)
            {
                foreach (var p in _pages) p.RefreshView();
                _pagesCV.Refresh();
            }
        }

        private void OnBookDeleted(object sender, EventArgs e)
        {
            if(_populated) Unpopulate();
            Book.CleanupEventHandlers();
        }

        private void OnBookDemoted(object sender, EventArgs e)
        {
            if(_populated) Unpopulate();
            Book.CleanupEventHandlers();
        }

        private void OnPagesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Replace)
            {
                _pagesCV.Refresh();
            }
            NotifyOfPropertyChange(() => PageCount);
        }

        public void OnTabDeactivated()
        {
            Unpopulate();
        }
        public void OnTabReactivated()
        {
            Model.PopulateAsync(IsSelected ? 1 : 2);
        }
        
        public void OnLibraryClosed()
        {
            if (_open) Close();
            if (_populated) Unpopulate();
        }

        /// <summary>
        /// Switched when Realised or Selected : Pages and PagesCV are loaded
        /// Cover Thumbnail is ordered and will be updated shortly.
        /// </summary>
        bool _populated;
        void Populate() 
        {
            if (_book.Pages == null || _book.Pages.Count < 1) return;

            _pages = new VmCollection<PageViewModel, BblPage>(_book.Pages, this);
            NotifyOfPropertyChange(() => Pages);
            _pagesCV = (ListCollectionView)CollectionViewSource.GetDefaultView(_pages);
            _pagesCV.IsLiveSorting = true;
            
            ((INotifyCollectionChanged)_pagesCV).CollectionChanged += OnPagesCollectionChanged;
            NotifyOfPropertyChange(() => PagesCV);
            _populated = true;

            if (IsSelected && !_open) Open();
        }

        //When Unrealized and Unselected
        public void Unpopulate()
        {
            if (_open) Close();
            _thumbRequested = false;
            _populated = false;
            if (_pagesCV != null)
            {
                ((INotifyCollectionChanged)_pagesCV).CollectionChanged -= OnPagesCollectionChanged;
                _pagesCV.DetachFromSourceCollection();
                _pagesCV = null;
            }
            if (Model.IsPopulated) Application.Current.Dispatcher.BeginInvokeIfRequired(DispatcherPriority.Background, new System.Action(() => {
                Model.UnPopulateAsync();
            })); 
            _pages = null;
            NotifyOfPropertyChange(() => Pages);
            NotifyOfPropertyChange(() => PagesCV);
        }
        
        public async Task UntilPopulated()
        {
            DateTime start = DateTime.Now;
            while (IsPopulated == false && (DateTime.Now - start).TotalSeconds < 5) await Task.Delay(25);
        }
        /// <summary>
        /// The book is opened / selected / pages displayed in the pages panel.
        /// </summary>
        bool _open;
        async void Open()
        {
            if(!IsPopulated)
            {
                await UntilPopulated();
                if (!IsPopulated) return;
            }
            Model.OnOpened();
            
            SortPages();
            ShowIvp = Library.Tab.TabState.showIvp;
            if(SelectedPage != null)
            {
                if (SelectedPage.IsSelected) SelectedPage.IsSelected = false;
                SelectedPage = null;
            }

            var prevSel = _pagesCV.CurrentItem as PageViewModel;

            if (_selectLastPageOnLoad) _pagesCV.MoveCurrentToLast();
            else _pagesCV.MoveCurrentToFirst();

            var sel = _pagesCV.CurrentItem as PageViewModel;
            if (prevSel == sel) prevSel = null;

            if (!sel.IsSelected)
            {
                if (prevSel != null) prevSel.IsSelected = false;
                sel.IsSelected = true;
            }
            else ScrollToPage(sel);
            if (SelectedPage != sel) SelectedPage = sel;
            
            _selectLastPageOnLoad = false;

            foreach (var p in _pages.Where(x => ((x.IsSelected || x.IsRealized) && !(x.Model.IsThumbnailLoaded || x.Model.IsImageLoading))))
            {
                p.Model.LoadImageAsync(IsSelected ? 1 : 2);
            }
            _open = true;

            NotifyOfPropertyChange(() => PagesCV);
        }
        void Close()
        {
            if(_open)
            {
                Model.OnClosed();
                _open = false;
                foreach (var p in _pages.Where(x => (x.Model.IsThumbnailLoaded || x.Model.IsImageLoaded)))
                {
                    if(p.Index != 0) p.Unload();
                    if (p.IsSelected) p.IsSelected = false;
                }
                GC.Collect();
            }
        }


        public void Collapse()
        {
            Library.Tab.BookVisible = false;
        }

        public PageViewModel GetFor(BblPage model, object context)
        {
            var pvm = new PageViewModel(model, this);
            model.ThumbnailLoaded += pvm.OnThumbnailLoaded;
            return pvm;
        }

        bool _selectLastPageOnLoad;
        public bool SelectFirstPage()
        {
            if (_populated && _open) 
            {
                var prevSel = _pagesCV.CurrentItem as PageViewModel;
                _pagesCV.MoveCurrentToFirst();
                var sel = _pagesCV.CurrentItem as PageViewModel;
                if (!sel.IsSelected)
                {
                    if (prevSel != null && prevSel.IsSelected) prevSel.IsSelected = false;
                    sel.IsSelected = true;
                }
                if (SelectedPage != sel) SelectedPage = sel;
            }
            return true;
        }
        public bool SelectLastPage()
        {
            if (!_populated || !_open) _selectLastPageOnLoad = true;
            else
            {
                var prevSel = _pagesCV.CurrentItem as PageViewModel;

                _pagesCV.MoveCurrentToLast();
                var sel = _pagesCV.CurrentItem as PageViewModel;
                if (!sel.IsSelected)
                {
                    if (prevSel != null && prevSel.IsSelected) prevSel.IsSelected = false;
                    sel.IsSelected = true;
                }
                if (SelectedPage != sel) SelectedPage = sel;
            }
            return true;
        }

        public bool SelectNextPage()
        {
            if (!_populated || !_open) return false;
            var prevSel = _pagesCV.CurrentItem as PageViewModel;
            if (_pagesCV.MoveCurrentToNext())
            {
                var sel = _pagesCV.CurrentItem as PageViewModel;
                if (!sel.IsSelected)
                {
                    if (prevSel != null && prevSel.IsSelected) prevSel.IsSelected = false;
                    sel.IsSelected = true;
                }
                if (SelectedPage != sel) SelectedPage = sel;
                return true;
            }
            else return false;
        }
        public bool SelectPreviousPage()
        {
            if (!_populated || !_open) return false;
            var prevSel = _pagesCV.CurrentItem as PageViewModel;
            if (_pagesCV.MoveCurrentToPrevious())
            {
                var sel = _pagesCV.CurrentItem as PageViewModel;
                if (!sel.IsSelected)
                {
                    if (prevSel != null && prevSel.IsSelected) prevSel.IsSelected = false;
                    sel.IsSelected = true;
                }
                if (SelectedPage != sel) SelectedPage = sel;
                return true;
            }
            else return false;
        }

        public async void GotoBookmark(BblBookmark bm)
        {
            if (bm.bookPath != Book.Path) return;
            DateTime start = DateTime.Now;
            while (!_populated && (DateTime.Now - start).TotalSeconds < 5) await Task.Delay(25);
            if (Pages == null || Pages.Count == 0) return;

            var p = Pages.Where(x => x.Model.Filename == bm.pageFilename).FirstOrDefault();
            if (p != null )
            {
                var prevSel = _pagesCV.CurrentItem as PageViewModel;
                if (_pagesCV.MoveCurrentTo(p))
                {
                    if (!p.IsSelected)
                    {
                        if (prevSel != null && prevSel.IsSelected) prevSel.IsSelected = false;
                        p.IsSelected = true;
                    }
                    if (SelectedPage != p) SelectedPage = p;
                }
            }
        }

        #region Sort Order
        static BindableCollection<string> _sortByCollection = new BindableCollection<string>() { "Natural", "Alphabetic", "Creation Time", "Last Modified" };
        public BindableCollection<String> SortBy => _sortByCollection;


        public String SelectedSortBy
        {
            get => (string.IsNullOrEmpty(Library.Tab.TabState.pagesSort)) ? "Natural" : Library.Tab.TabState.pagesSort;
            set
            {
                Library.Tab.TabState.pagesSort = value;
                NotifyOfPropertyChange(() => SelectedSortBy);
                SortPages();
            }
        }

        
        public bool SortDirection
        {
            get => Library.Tab.TabState.pagesSortDirection; 
            set
            {
                Library.Tab.TabState.pagesSortDirection = value;
                NotifyOfPropertyChange(() => SortDirection);
                SortPages();
            }
        }
        private void SortPages()
        {
            _pagesCV.SortDescriptions.Clear();
            if (SelectedSortBy == "Natural")
                _pagesCV.SortDescriptions.Add(new SortDescription("Index", SortDirection ? ListSortDirection.Ascending : ListSortDirection.Descending));
            else if (SelectedSortBy == "Alphabetic")
                _pagesCV.SortDescriptions.Add(new SortDescription("Path", SortDirection ? ListSortDirection.Ascending : ListSortDirection.Descending));
            else if (SelectedSortBy == "Creation Time")
                _pagesCV.SortDescriptions.Add(new SortDescription("CreationTime", SortDirection ? ListSortDirection.Ascending : ListSortDirection.Descending));
            else if (SelectedSortBy == "Last Modified")
                _pagesCV.SortDescriptions.Add(new SortDescription("LastWriteTime", SortDirection ? ListSortDirection.Ascending : ListSortDirection.Descending));
            _pagesCV.Refresh();
        }
        #endregion


        #region Rename/Delete this book
        public void StartRenaming()
        { Renaming = true; }

        public void RenameFile()
        {
            if (_book.Name != _name)
            {
                _book.RenameFile(_name);
            }
            Renaming = false;
        }

        public void MoveFile(string parentDirectory)
        {
            MessageBoxResult r = MessageBox.Show(string.Format("Move {0} \nto {1} ?", Path, parentDirectory), "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes)
            {
                Book.MoveFile(parentDirectory);
            }
        }

        public void DeleteFile()
        {
            System.Windows.MessageBoxResult r = System.Windows.MessageBox.Show(string.Format("Send {0} to the Recycle Bin?", (object)Path), "Confirmation",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            if (r == System.Windows.MessageBoxResult.Yes)
            {
                Book.DeleteFile();
            }
        }

        private bool _renaming;
        public bool Renaming
        {
            get { return _renaming; }
            set { _renaming = value; NotifyOfPropertyChange(() => Renaming); }
        }
        public void RenameTextBoxKeyDown(ActionExecutionContext context)
        {
            var keyArgs = context.EventArgs as KeyEventArgs;

            if (keyArgs != null && keyArgs.Key == Key.Enter)
            {
                RenameFile();
                Name = _book.Name;
                Renaming = false;
                keyArgs.Handled = true;
            }
            if (keyArgs != null && keyArgs.Key == Key.Escape)
            {
                Name = _book.Name;
                Renaming = false;
                keyArgs.Handled = true;
            }
            
        }
        public void RenameTextBoxLostFocus()
        {
            if (Renaming == true)
            {
                if (Name != _book.Name)
                {
                    RenameFile();
                    Name = _book.Name;
                }
                Renaming = false;
            }
        }
        #endregion

        #region Rename/Delete pages
        public void LV_KeyDown(ActionExecutionContext context)
        {
            var keyArgs = context.EventArgs as KeyEventArgs;

            if (keyArgs != null && keyArgs.Key == Key.F2 && CanRename)
            {
                var p = (PageViewModel)PagesCV.CurrentItem;
                p.Renaming = true;
                keyArgs.Handled = true;
            }
            if (keyArgs != null && keyArgs.Key == Key.Delete && _book.CanDeletePages)
            {
                if (PagesCV != null && PagesCV.CurrentItem != null)
                {
                    if (_selectedPages != null)
                    {
                        List<PageViewModel> pages = _selectedPages.Cast<PageViewModel>().ToList();
                        if (pages.Count > 1)
                        {
                            System.Windows.MessageBoxResult r =  System.Windows.MessageBox.Show(
                                string.Format("Send {0} selected pages to the Recycle Bin?", pages.Count.ToString()), "Confirmation",
                                System.Windows.MessageBoxButton.YesNoCancel, System.Windows.MessageBoxImage.Question);
                            if(r == MessageBoxResult.Yes)
                            {
                                foreach (var p in pages) p.DeleteFile(true);
                            }
                        }
                        else
                        {
                            var p = (PageViewModel)PagesCV.CurrentItem;
                            p.DeleteFile();
                        }
                        keyArgs.Handled = true;
                    }
                }
            }
        }
        //TODO Datacontext for these 2 event bindins is wrong. should go directly to selected page methods
        //xaml in BookView : line 90
        public void RenamePageTextBoxKeyDown(ActionExecutionContext context)
        {
            _selectedPage.RenamePageTextBoxKeyDown(context);
        }
        public void RenamePageTextBoxLostFocus()
        {
            _selectedPage.RenamePageTextBoxLostFocus();
        }

        public void RenamePage(PageViewModel page, string fileName)
        {
            page.Model.RenameFile(fileName);
        }

        private string _batchRenamePrefix;
        public String BatchRenamePrefix
        {
            get { return _batchRenamePrefix; }
            set { _batchRenamePrefix = value; NotifyOfPropertyChange(() => BatchRenamePrefix); }
        }
        public void SetBatchRenamePrefixToBookName()
        {
            BatchRenamePrefix = System.IO.Path.GetFileNameWithoutExtension(Path);
        }
        public async void BatchRename()
        {
            _pagesCV.MoveCurrentTo(-1);
            var list = PagesCV.Cast<PageViewModel>().ToList();
            string fmt = "0";
            int digits = list.Count.ToString().Length;
            for (int j = 1; j < digits; j++) fmt += "0";
            Dictionary<string, PageViewModel> test = new Dictionary<string, PageViewModel>();
            int i = 0;
            foreach (var pvm in list)
            {
                var newName = _batchRenamePrefix + (++i).ToString(fmt) + pvm.Model.FileExtension;
                test.Add(newName, pvm);
            }

            string prefix = "";
            while (true)
            {
                bool namesAreNotTaken = true;
                foreach (var f in Directory.EnumerateFiles(Path))
                {
                    string fileName = f.Substring(Path.Length + 1);
                    if (test.ContainsKey(prefix + fileName))
                    {
                        namesAreNotTaken = false;
                        break;
                    }
                }
                if (namesAreNotTaken || prefix.Length==10) break;
                else prefix += "_";
            }
            if(prefix.Length == 10)
            { 
                MessageBox.Show("Some Destination Files already exists! Find better prefix", "Rename Failure", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation);
                return;
            }
            BookProgressMax = test.Count;
            BookProgress = 0;
            foreach (var kv in test)
            { 
                if (!kv.Value.Model.RenameFile(prefix + kv.Key, false))
                {
                    MessageBox.Show("Operation Failed", "Rename Failure", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation);
                    break;
                }
                BookProgress = BookProgress + 1;
                await Task.Delay(5);
            }
            BookProgress = 0;
            BookProgressMax = 100;
            SortPages();
        }
        public bool CanRename { get { return _book.CanRenamePages; } }
        public Visibility CanDisplayBatchRename { get { return (_book.CanRenamePages) ? Visibility.Visible : Visibility.Hidden; } }

        #endregion

        public void ReorderCreationTimes()
        {
            var list = PagesCV.Cast<PageViewModel>().ToList();
            BookProgress = 0;
            BookProgressMax = list.Count * 2;
            DateTime first = list[0].Model.CreationTime;
            DateTime last = list[list.Count-1].CreationTime;

            foreach(var p in list)
            {
                if (first > p.Model.CreationTime) first = p.Model.CreationTime;
                if (last < p.Model.CreationTime) last = p.Model.CreationTime;
                
            }

            double interval = (last - first).TotalMilliseconds;
            interval /= list.Count;
            if (interval < 1) interval = 1;

            foreach (var p in list)
            {
                try {  if (File.Exists(p.Model.Path)) File.SetCreationTime(p.Model.Path, first); }
                catch { }
                p.Model.CreationTime = first;
                first.AddMilliseconds(interval);
                BookProgress = BookProgress + 1;
            }
            BookProgress = 0;
            BookProgressMax = 100;
        }

        public ICommand ShowDetailsCommand { get { return new DelegateCommand(new Action<object>((t) => { DoShowDetailCommand(); })); } }

        public void DoShowDetailCommand()
        {
            ShellViewModel.Instance.ToggleFullscreen();
        }

        public void OpenInExplorer()
        {
            string dir = "";
            FileInfo info = new FileInfo(Model.Path);
            FileAttributes attr = info.Attributes;
            if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                dir = info.FullName;
            else
                dir = info.DirectoryName;


            try { System.Diagnostics.Process.Start(dir); }
            catch { }
        }
        public bool CanOpenInExplorer => !string.IsNullOrEmpty(Model.Path);
        public void OpenFile()
        {
            try { System.Diagnostics.Process.Start(Model.Path); }
            catch { }
        }
        public bool CanOpenFile => (Model.Type == BblBook.BookType.Directory) ? Directory.Exists(Model.Path) : File.Exists(Model.Path);


        //public async void AddToDeepVDataSet()
        //{
        //    BookProgressMax = _pages.Count;
        //    BookProgress = 0;
        //    foreach (PageViewModel p in _pages)
        //    {
        //        DeepVBuilderData data = new DeepVBuilderData();
        //        data.path = p.Model.Path;
        //        data.ivpAngle = (float)Math.Round(p.Model.Ivp.rotation, 2);
        //        data.ivpLeft = (float)Math.Round(Utility.Clamp(p.Model.Ivp.l, 0f, 1f), 2);
        //        data.ivpRight = (float)Math.Round(Utility.Clamp(p.Model.Ivp.r, 0f, 1f), 2);
        //        data.ivpTop = (float)Math.Round(Utility.Clamp(p.Model.Ivp.t, 0f, 1f), 2);
        //        data.ivpBottom = (float)Math.Round(Utility.Clamp(p.Model.Ivp.b, 0f, 1f), 2);
        //        await Task.Run(() => DVDataSetMgr.AddDeepVEntry(ref data));
        //        BookProgress = BookProgress + 1;
        //    }
        //    BookProgress = 0;
        //}

    }

    public class BookViewModelComparer : IComparer
    {
        public CustomSort sortBy = new CustomSort();
        public bool sortDirection = true;
        public HashSet<String> tags = new HashSet<String>();

        public int Compare(object x, object y)
        {
            if (sortBy == null) return 0;

            var X = x as BookViewModel;
            var Y = y as BookViewModel;

            int result = 0;

            for (int k = 0; k < sortBy.Value.Count; k++)
            {
                bool dir = sortBy.Value[k].Direction;
                dir = (sortDirection) ? dir : !dir;

                switch (sortBy.Value[k].Field)
                {
                    case "":
                    case "Natural":
                        {
                            result = X.Index.CompareTo(Y.Index);
                            if (result != 0) return (dir) ? result : -result;
                            break;
                        }
                    case "Name":
                        {
                            result = String.Compare(X.Name, Y.Name);
                            if (result != 0) return (dir) ? result : -result;
                            break;
                        }
                    case "Path":
                        {
                            result = String.Compare(X.Path, Y.Path);
                            if (result != 0) return (dir) ? result : -result;
                            break;
                        }
                    case "Creation Time":
                        {
                            result = DateTime.Compare(X.CreationTime, Y.CreationTime);
                            if (result != 0) return (dir) ? result : -result;
                            break;
                        }
                    case "Last Modified":
                        {
                            result = DateTime.Compare(X.LastWriteTime, Y.LastWriteTime);
                            if (result != 0) return (dir) ? result : -result;
                            break;
                        }

                    case "Randomized":
                        {
                            result = X.RandomInt.CompareTo(Y.RandomInt);
                            if (result != 0) return (dir) ? result : -result;
                            break;
                        }
                    default:
                        {
                            var xtags = X.Tags;
                            var ytags = Y.Tags;
                            foreach (var tag in tags)
                            {
                                if (tag == sortBy.Value[k].Field)
                                {
                                    BblTag xtag = (xtags.Count > 0) ? xtags.Where(z => z.Key == tag).FirstOrDefault() : null;
                                    string xval = (xtag != null) ? xtag.Value : "";

                                    BblTag ytag = (ytags.Count > 0) ? ytags.Where(z => z.Key == tag).FirstOrDefault() : null;
                                    string yval = (ytag != null) ? ytag.Value : "";

                                    result = String.Compare(xval, yval);
                                    if (result != 0) return (dir) ? result : -result;
                                }
                            }
                            break;
                        }
                }
            }
            return 0;
        }
    }

    public static class ThreadSafeRandom
    {
        [ThreadStatic]
        private static Random Local;

        public static Random ThisThreadsRandom
        {
            get { return Local ?? (Local = new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId))); }
        }
    }
}

