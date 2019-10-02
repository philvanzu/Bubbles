using Bubbles3.Models;
using Bubbles3.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using Caliburn.Micro;
using System.Collections;
using System.Windows.Input;
using System.Windows;
using System.Windows.Threading;

namespace Bubbles3.ViewModels
{
    public class LibraryViewModel : PropertyChangedBase, IViewModelProvider<BookViewModel, BblBook>
    {

        private BblLibrary _model;
        public BblLibrary Model => _model;
        private static ObservableCollection<BookViewModel> _emptybookscollection = new ObservableCollection<BookViewModel>();
        public BookViewModelComparer comparer = new BookViewModelComparer();


        public LibraryViewModel(TabViewModel tab)
        {
            Tab = tab;
        }

        public TabViewModel Tab { get; set; }

        /// <summary>
        /// VMCollections interface responsible for the creation of
        /// a BookViewModel corresponding to the provided BblBook
        /// which was just added to the VMCollection
        /// </summary>
        public BookViewModel GetFor(BblBook model, object context)
        {
            BookViewModel bvm = new BookViewModel(model, this);
            return bvm;
        }



        List<BookViewModel> _loadedBooks;
        public void OnTabDeactivated(bool close)
        {
            if (close) Close();
            else
            {
                _loadedBooks = Books.Where(b => b.IsRealized || b.IsSelected).ToList();
                foreach (var bvm in _loadedBooks) bvm.OnTabDeactivated();
            }
        }

        public void OnTabReactivated()
        {
            foreach (var bvm in _loadedBooks) bvm.OnTabReactivated();
            _loadedBooks = null;
        }
        
        //Load a directory when TabViewModel.SelectedFolder is changed.
        public void Open(DirectoryInfoEx dir)
        {
            if (_model != null) CloseLibrary();

            //open
            _model = new BblLibrary(dir);
            _model.LibraryLoaded += OnLibraryLoaded;

            Tab.ProgressValue = 0;
            Tab.ProgressMaximum = 100;
            Tab.ProgressIsDeterminate = true;

            _books = new VmCollection<BookViewModel, BblBook>(_model.Books, this);
            NotifyOfPropertyChange(() => Books);


            _booksCV = (ListCollectionView)CollectionViewSource.GetDefaultView(_books);
            ((INotifyCollectionChanged)(_booksCV)).CollectionChanged += OnBooksCollectionChanged;
            NotifyOfPropertyChange(() => BooksCV);

            Tab.ResetSortUI();
            Tab.TabState.currentBookmark.libraryPath = dir.FullName;
            _booksCV.CustomSort = comparer;
        }
        void Close()
        {
            if (_books == null) return;
            foreach (var bvm in _books) bvm.Unpopulate();
            _booksCV.DetachFromSourceCollection();
            _booksCV = null;
            CloseLibrary();
            _books = null;
            Tab.TabState.currentBookmark.libraryPath = null;
        }

        private void OnLibraryLoaded(object sender, EventArgs e)
        {
            _model.LibraryLoaded -= OnLibraryLoaded;
            Tab.ProgressIsDeterminate = false;

            if (_booksCV.CurrentPosition == -1)
            {
                var book = (BookViewModel)_booksCV.GetItemAt(0);
                SelectWhenPopulated(book);
            }
        }
        public void CloseLibrary()
        {
            foreach (var b in Books) b.OnLibraryClosed();
            if (_model == null) return;
            _model.CloseLibrary(Tab.SaveToDBToggle);
            _model = null;
        }



        private void OnBooksCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            //if (e.Action == NotifyCollectionChangedAction.Add && e.NewStartingIndex == 0) { }
            //else if (e.Action == NotifyCollectionChangedAction.Remove){ }
            //else if (e.Action == NotifyCollectionChangedAction.Replace) { }
            Tab.NotifyOfPropertyChange(() => Tab.BooksCount);
            
        }

        public async void SelectWhenPopulated(BookViewModel book)
        {
            DateTime start = DateTime.Now;
            while (book.IsPopulated == false && (DateTime.Now -start).TotalSeconds < 5) await Task.Delay(25);
            if(book.IsRealized) SelectBook(book);
        }



        private VmCollection<BookViewModel, BblBook> _books;
        public ObservableCollection<BookViewModel> Books => (_model != null) ? _books : _emptybookscollection;
        private ListCollectionView _booksCV;
        public ListCollectionView BooksCV => _booksCV;


        BookViewModel _selectedBook;
        public BookViewModel SelectedBook
        {
            get => _selectedBook; 
            set {
                if(value != _selectedBook)
                {
                    _selectedBook = (BookViewModel)value;
                    Tab.OnBookSelected(value);

                    //preload next / previous row of books if it is not realized by bringing them to view => populating them in idle time
                    //shortening the wait the next time RequestNextBook / RequestPreviousBook is called.
                    if(value != null)
                    {
                        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new System.Action(async () =>
                        {
                            await (_booksCV.CurrentItem as BookViewModel).UntilPopulated();
                            if(_booksCV != null && _booksCV.CurrentPosition < _booksCV.Count -1)
                            {
                                int nextId = _booksCV.CurrentPosition + 1;
                                var next = (BookViewModel)_booksCV.GetItemAt(nextId);
                                if (!next.IsRealized) ScrollToIndex = nextId;
                                else if (_booksCV.CurrentPosition > 0)
                                {
                                    int prevId = _booksCV.CurrentPosition - 1;
                                    var prev = (BookViewModel)_booksCV.GetItemAt(prevId);
                                    if (!prev.IsRealized) ScrollToIndex = prevId;
                                }
                            }
                        }));
                    }
                }
                NotifyOfPropertyChange(() => SelectedBook);
            }
        }

        IEnumerable _selectedBooks;
        public IEnumerable SelectedBooks
        {
            get { return _selectedBooks; }
            set
            {
                _selectedBooks = value;
            }
        }


        private double _scrollToIndex;
        public double ScrollToIndex
        {
            get { return _scrollToIndex; }
            set { _scrollToIndex = value; NotifyOfPropertyChange(() => ScrollToIndex); }
        }
        public void ScrollToSelected()
        {
            if (BooksCV != null)
                ScrollToIndex = BooksCV.CurrentPosition;
        }

        public void ScrollToBook(BookViewModel book)
        {
            if (BooksCV != null)
                ScrollToIndex = BooksCV.IndexOf(book);
        }

        private LibraryFilter _filter = null;
        public void OnFilterEntered(string inputFilterString)
        {
            string currentFilterString = (_filter != null) ? _filter.ToString() : "";

            if (String.IsNullOrEmpty(inputFilterString))
            {
                _booksCV.Filter = null;
                _filter = null;
            }
            else
            {
                _filter = new LibraryFilter(inputFilterString);
                _booksCV.Filter = FilterLibrary;
            }
            _booksCV.Refresh();

            currentFilterString = (_filter != null) ? _filter.ToString() : "";
        }

        public bool FilterLibrary(object item)
        {
            if (item == null) return false;
            BookViewModel b = item as BookViewModel;
            try
            {
                return _filter.Filter(b);
            }
            catch (Exception e)
            { Console.WriteLine(e.Message); }

            return false;
        }



        
        
        public void SortBooks()
        {
            if (BooksCV == null) return;
            if (comparer.sortBy.Name == "Randomized") foreach (var b in Books) b.RefreshRandomInt();
            BooksCV.CustomSort = comparer;
            BooksCV.Refresh();
        }


        #region ListView KeyDown handler
        public void LV_KeyDown(ActionExecutionContext context)
        {
            var keyArgs = context.EventArgs as KeyEventArgs;

            if (keyArgs != null && keyArgs.Key == Key.F2)
            {
                var b = (BookViewModel)BooksCV.CurrentItem;
                keyArgs.Handled = true;
                b.Renaming = true;
            }
            if (keyArgs != null && keyArgs.Key == Key.Delete)
            {
                if (BooksCV != null && BooksCV.CurrentItem != null)
                {
                    var b = (BookViewModel)BooksCV.CurrentItem;
                    keyArgs.Handled = true;
                    b.DeleteFile();
                }
            }
        }
        #endregion

        public bool SelectNextBook()
        {
            var prevSel = _booksCV.CurrentItem as BookViewModel;
            if (_booksCV.MoveCurrentToNext() || _booksCV.MoveCurrentToFirst())
            {
                var sel = _booksCV.CurrentItem as BookViewModel;
                if (!sel.IsSelected)
                {
                    if (prevSel != null && prevSel.IsSelected) prevSel.IsSelected = false;
                    sel.IsSelected = true;
                }
                if (SelectedBook != sel) SelectedBook = sel;
                return SelectedBook.SelectFirstPage();
            }
            return false;
        }

        public bool SelectPreviousBook()
        {
            var prevSel = _booksCV.CurrentItem as BookViewModel;
            if (_booksCV.MoveCurrentToPrevious() || _booksCV.MoveCurrentToLast())
            {
                var sel = _booksCV.CurrentItem as BookViewModel;
                if (!sel.IsSelected)
                {
                    if (prevSel != null && prevSel.IsSelected) prevSel.IsSelected = false;
                    sel.IsSelected = true;
                }
                if (SelectedBook != sel) SelectedBook = sel;
                return SelectedBook.SelectLastPage();
            }
            return false;
        }

        public void SelectBook(BookViewModel book)
        {
            if(book != null && !book.IsSelected)
            {
                //if (!book.IsRealized)
                //{
                //    SelectedBook = null;
                //    _selectOnRealized = book;
                //    return;
                //}

                var prevSel = _booksCV.CurrentItem as BookViewModel;
                if (!book.IsSelected)
                {
                    if (prevSel != null && prevSel.IsSelected) prevSel.IsSelected = false;
                    book.IsSelected = true;
                }
                if (SelectedBook != book) SelectedBook = book;
            }
        }
        public void GotoBookmark(BblBookmark bm)
        {
            if (Model.Root.Path != bm.libraryPath) return;

            var book = Books.Where(x => x.Model.Path == bm.bookPath).FirstOrDefault();
            if (book != null) SelectBook(book);
            book.GotoBookmark(bm);
        }
        public void Filter(string query)
        {
            string filter = (_filter != null) ? _filter.ToString() : "";

            if (string.IsNullOrEmpty(query))
            {
                _booksCV.Filter = null;
                _filter = null;
            }
            else
            {
                _filter = new LibraryFilter(query);
                _booksCV.Filter = FilterLibrary;
            }
            _booksCV.Refresh();

            filter = (_filter != null) ? _filter.ToString() : "";
        }

        public void AddBookDirectories()
        {
            
        }
    }
}
