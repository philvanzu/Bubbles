using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Bubbles4.Models;
using Bubbles4.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;

namespace Bubbles4.ViewModels;

public partial class LibraryViewModel : ViewModelBase, ISelectItems
{
    public string Path { get; }
    
    private readonly List<BookViewModel> _books = new();
    private ObservableCollection<BookViewModel> _booksMutable = new();
    public ReadOnlyObservableCollection<BookViewModel> Books { get; }
    [ObservableProperty] BookViewModel? _selectedItem;
    public LibraryConfig.SortOptions CurrentSortOption { get; set; }
    public bool CurrentSortAscending { get; set; }

    private List<string>? _filters;
    public int Count => Books.Count;
    public MainViewModel MainViewModel { get; set; }
    public event EventHandler<SelectedItemChangedEventArgs>? SelectionChanged;
    public event EventHandler? SortOrderChanged;
    public event EventHandler<int>? ScrollToIndexRequested;
    
    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private LibraryNodeViewModel _rootNode;
    private LibraryConfig _config;
    public LibraryConfig Config
    {
        get => _config;
        set
        {
            bool changed = _config != value;
            if (changed)
            {
                _config.Bookmarks.CollectionChanged -= OnBookmarksCollectionChanged;
                value.Bookmarks.CollectionChanged += OnBookmarksCollectionChanged;
            }
            SetProperty(ref _config, value);
            MainViewModel.LibrarySortHeader.Value = (value.LibrarySortOption, value.LibrarySortAscending);
            MainViewModel.BookSortHeader.Value = (value.BookSortOption, value.BookSortAscending);
            MainViewModel.NodeSortHeader.Value = (value.NodeSortOption, value.NodeSortAscending);
            MainViewModel.PreviewIVPIsChecked = false;
            MainViewModel.ShowNavPane = value.ShowNavPane;
        }
    }
    [ObservableProperty] private ReadOnlyObservableCollection<string> _bookmarks;

    [ObservableProperty] private LibraryNodeViewModel? _selectedNode;

    partial void OnSelectedNodeChanged(LibraryNodeViewModel? value)
    {
        if (value != null)
        {
            Sort();
        }
    }
    
    public LibraryViewModel(MainViewModel mainViewModel, string path, LibraryConfig config)
    {
        MainViewModel = mainViewModel;
        Path = path;
        _config = config;
        _config.Bookmarks.CollectionChanged += OnBookmarksCollectionChanged;
        var inner = new ObservableCollection<string>(_config.Bookmarks.Select(x => x.BookPath).ToList());
        Bookmarks = new(inner);

        Books = new ReadOnlyObservableCollection<BookViewModel>(_booksMutable);

        CurrentSortOption = Config.LibrarySortOption;
        CurrentSortAscending = Config.LibrarySortAscending;
        
        var info  = new DirectoryInfo(path);
        RootNode = new LibraryNodeViewModel(this,info.FullName, info.Name, info.CreationTime, info.LastWriteTime);
        Config = _config;
    }



    public virtual void Close()
    {
        //__book0__issue__hack
        //if( Books.Count != 0 )Books[0].Thumbnail?.Dispose();
        
        if(SelectedItem != null) 
            SelectedItem.IsSelected = false;
        
        SelectedItem = null;
        
        Clear();
        MainViewModel.ShowNavPane = false;
    }
    public void Clear()
    {
        //__book0__issue__hack
        //if( Books.Count != 0 )Books[0].Thumbnail?.Dispose();
        _books.Clear();
        _booksMutable.Clear();
        OnPropertyChanged(nameof(Count));
    }

    

    public int NodeBooksCount(string libraryNodeId)=> _books.Count(b => b.LibraryNodeId == libraryNodeId);
    
    


    [RelayCommand]
    private void BookPrepared(object? parameter)
    {
        if (parameter is BookViewModel vm)
        {
            /*
            if (vm.Thumbnail != null)
            {
                OnPropertyChanged(nameof(vm.Thumbnail));
                return;
            }
            */
            vm.PrepareThumbnail();
        }
            
    }

    [RelayCommand]
    private void BookClearing(object? parameter)
    {
        if (parameter is BookViewModel vm)
        {
            //Console.WriteLine($"Clearing Thumbnail for book: {vm.Path}");
            vm.ClearThumbnail();    
        }
            
    }
    

    


    private IComparer<BookViewModel> GetComparer(LibraryConfig.SortOptions sort, bool ascending)
    {
        return sort switch
        {
            LibraryConfig.SortOptions.Path => ascending
                ? SortExpressionComparer<BookViewModel>.Ascending(x => x.Path)
                : SortExpressionComparer<BookViewModel>.Descending(x => x.Path),

            LibraryConfig.SortOptions.Alpha => ascending
                ? SortExpressionComparer<BookViewModel>.Ascending(x => x.Name)
                : SortExpressionComparer<BookViewModel>.Descending(x => x.Name),

            LibraryConfig.SortOptions.Created => ascending
                ? SortExpressionComparer<BookViewModel>.Ascending(x => x.Created)
                : SortExpressionComparer<BookViewModel>.Descending(x => x.Created),

            LibraryConfig.SortOptions.Modified => ascending
                ? SortExpressionComparer<BookViewModel>.Ascending(x => x.LastModified)
                : SortExpressionComparer<BookViewModel>.Descending(x => x.LastModified),

            LibraryConfig.SortOptions.Natural => new BookViewModelNaturalComparer(ascending),

            LibraryConfig.SortOptions.Random => 
                SortExpressionComparer<BookViewModel>.Ascending(x => x.RandomIndex),

            _ => SortExpressionComparer<BookViewModel>.Ascending(x => x.Name)
        };
    }

    private void ShuffleBooks()
    {
        foreach (var book in _books)
            book.RandomIndex = CryptoRandom.NextInt();
    }
    public void Sort(LibraryConfig.SortOptions? sort=null, bool? ascending=null)
    {
        if (sort == null) sort = Config.LibrarySortOption;
        if (ascending == null) ascending = Config.LibrarySortAscending;

        if (sort == LibraryConfig.SortOptions.Random) ShuffleBooks();

        CurrentSortOption = sort.Value;
        CurrentSortAscending = ascending.Value;
        ApplyFilterAndSort();
        //InvokeSortOrderChanged();
    }
    public void Filter(List<string>? keywords = null)
    {
        _filters = keywords;
        ApplyFilterAndSort();
    }
    private void ApplyFilterAndSort()
    {
        var comparer = GetComparer(CurrentSortOption, CurrentSortAscending);

        var filtered = _books
            .Where(b => MatchesKeywords(b, _filters))
            .OrderBy(b => b, comparer);

        //__book0__issue__hack
        //if( Books.Count != 0 )Books[0].Thumbnail?.Dispose();
        
        _booksMutable.Clear();
        _booksMutable.AddRange(filtered);

        OnPropertyChanged(nameof(Books));
    }
    
    public void InvokeSortOrderChanged()
    {
        SortOrderChanged?.Invoke(this, EventArgs.Empty);
    }
    private bool MatchesKeywords(BookViewModel bvm, List<string>? keywords)
    {
        if (keywords == null && Config.Recursive == false)
            if (SelectedNode == null || SelectedNode.Path != bvm.LibraryNodeId) return false;

        if (keywords == null || keywords.Count == 0)
            return true;
        
        
        foreach (var keyword in keywords)
        {
            if (bvm.Path.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }
        return true;
    }

    public void ReverseSortOrder()
    {
        CurrentSortAscending = !CurrentSortAscending;
        Sort(CurrentSortOption, CurrentSortAscending);
    }



    partial void OnSelectedItemChanged(BookViewModel? value)
    {
        BookViewModel? oldItem = null;
        MainViewModel.SelectedBook = value;
        foreach (var book in Books)
        {
            if (book != value && book.IsSelected)
            {
                oldItem = book;
                //unload book in BookView and dispose pages data
                
                book.IsSelected = false;
            }
        }
        if (value != null)
        {
            value.LoadingTask = value.LoadPagesListAsync();
            var node = RootNode.FindNode(value.LibraryNodeId);
            if (node != null && SelectedNode != node) 
                SelectedNode = node;
            //load book in bookview
            _ = Task.Run(async () =>
                {
                    try { await value.LoadingTask;}
                    catch(Exception x){Console.WriteLine(x);}                    
                    finally {value.LoadingTask = null;}
                });
        }
        InvokeSelectionChanged(SelectedItem, oldItem);
        MainViewModel.UpdateBookStatus();
        //Console.WriteLine("Selected book :" + value.Name );
    }
    public void InvokeSelectionChanged(ISelectableItem? newItem, ISelectableItem? oldItem)
    {
        SelectionChanged?.Invoke(this, new SelectedItemChangedEventArgs(this,  newItem, oldItem));
    }
    
    public void RequestScrollToIndex(int index)
    {
        ScrollToIndexRequested?.Invoke(this, index);
    }
    public int GetSelectedIndex()
    {
        return GetBookIndex(SelectedItem);
    }
    public int GetBookIndex(BookViewModel? book)
    {
        return (book != null)? Books.IndexOf(book) : -1;
    }
    
    public void NextBook()
    {
        var index = GetBookIndex(SelectedItem);
        if (Books.Count <= 0) return;
        if (index == -1) index = 0;
        else
        {
            int newIndex = index + 1;
            if (newIndex >= Books.Count) newIndex = 0;
            index = newIndex;
        }

        if (Books[index].IsSelected ) Books[index].SelectedPage = null;
        else Books[index].IsSelected = true;
        
    }

    public void PreviousBook()
    {
        var index = GetBookIndex(SelectedItem);
        if (Books.Count <= 0) return;
        if (index == -1) index = Books.Count - 1;
        else
        {
            int newIndex = index - 1;
            if (newIndex < 0) newIndex = Books.Count - 1;
            index = newIndex;
        }
        if (Books[index].IsSelected) Books[index].SelectedPage = null;
        else Books[index].IsSelected = true;
    }
    private void OnBookmarksCollectionChanged(object? _, NotifyCollectionChangedEventArgs __)
    {
        var inner = new ObservableCollection<string>(_config.Bookmarks.Select(x => x.BookPath).ToList());
        Bookmarks = new(inner);
    }

    [RelayCommand]
    private async Task LoadBookmark(string bookPath)
    {
        var bm = Config.GetBookmark(bookPath);
        if(bm != null)
        {
            var book = _books.FirstOrDefault(b => b.Path == bookPath);
            if (book != null)
            {
                book.IsSelected = true;

                
                if (book.LoadingTask != null)
                {
                    await book.LoadingTask;
                    if(book.Pages.Count > bm.pageIndex)
                        book.Pages[bm.pageIndex].IsSelected = true;
                }
            }    
        }
        
    }

    #region FileSystem Watcher Events

    public virtual void FileSystemChanged(FileSystemEventArgs e)
    {
        bool sort = false;
        BookBase? newBook = null;
        BookViewModel? existing = null;
        BookViewModel? shouldRemove = null;
        BookViewModel? shouldAdd = null;
        (LibraryNodeViewModel, string)? shouldAddNode = null;
        LibraryNodeViewModel? shouldRemoveNode = null;
        
        bool removeFlag = false;
        
        bool isImage = FileAssessor.IsImage(e.FullPath);
        if (isImage)
        {
            string? imgDir = System.IO.Path.GetDirectoryName(e.FullPath);
            if (imgDir is not null)
            {
                var bvm = _books.FirstOrDefault(x => string.Equals(x.Path, imgDir, StringComparison.OrdinalIgnoreCase));
                if (bvm is not null)
                {
                    if(bvm.Pages.Count > 0) 
                        bvm.PageFileChanged(e);
                }
                else if(e.ChangeType == WatcherChangeTypes.Created && Directory.Exists(imgDir))
                {
                    string? nodePath = System.IO.Path.GetDirectoryName(imgDir);
                    if (nodePath is null) return;
                    FileInfo info = new FileInfo(imgDir);
                    var bd = new BookDirectory(info.FullName, info.Name, -1,info.LastWriteTime, info.CreationTime);
                    //!! potentially creating many versions of the same bookdirectory, but they should be bounced off in the processing thread if it happens
                    shouldAdd = new BookViewModel(bd, this, nodePath);    
                }
            }
        }

        else
        {
            existing = _books.FirstOrDefault(x => string.Equals(x.Path, e.FullPath, StringComparison.OrdinalIgnoreCase));
            bool isArchive = FileAssessor.IsArchive(e.FullPath);
            bool isPdf = FileAssessor.IsPdf(e.FullPath);
            bool isDirbook = FileAssessor.IsImageDirectory(e.FullPath);
   
            bool isBook = isDirbook || isPdf || isArchive;
            if (!isBook && existing is not null)
            {
                isBook = true;
                if(existing.Model is BookDirectory) isDirbook = true;
                else if(existing.Model is BookArchive) isArchive = true;
                else if(existing.Model is BookPdf) isPdf = true;
            }

            if (e.ChangeType == WatcherChangeTypes.Changed && existing != null)
            {
                existing.FileSystemChanged(e);
                sort = true;
            }
            else
            {
                if (e.ChangeType == WatcherChangeTypes.Created )
                {
                    if (isArchive)
                    {
                        FileInfo info = new FileInfo(e.FullPath);
                        newBook = new BookArchive(info.FullName, info.Name, -1, info.LastWriteTime, info.CreationTime);
                        removeFlag = true;
                    }
                    else if (isPdf)
                    {
                        FileInfo info = new FileInfo(e.FullPath);
                        newBook = new BookPdf(info.FullName, info.Name, -1, info.LastWriteTime, info.CreationTime);
                        removeFlag = true;
                    }
                    else if (isDirbook)
                    {
                        DirectoryInfo info = new DirectoryInfo(e.FullPath);
                        newBook = new BookDirectory(info.FullName, info.Name, -1, info.LastWriteTime, info.CreationTime);
                        removeFlag = true;
                    }
                }
                else if (e.ChangeType == WatcherChangeTypes.Deleted)
                {
                    if (isArchive || isPdf || isDirbook) removeFlag = true;
                }

                if (removeFlag && existing != null) shouldRemove = existing;
                if (newBook != null)
                {
                    string? nodePath = System.IO.Path.GetDirectoryName(newBook.Path);
                    if (nodePath is null) return;
                    shouldAdd = new BookViewModel(newBook, this, nodePath);
                }
            }
        }
        if (shouldAdd != null || shouldRemove != null || sort)
        {
            EnqueueWatcherEvent((shouldAdd, shouldRemove, sort));
        }
    }

    private ConcurrentQueue<(BookViewModel? add, BookViewModel? remove, bool sort )> _watcherProcessQueue = new(); 
    
    private int _watcherRunning;

    private void EnqueueWatcherEvent((BookViewModel? add, BookViewModel? remove, bool sort) item)
    {
        Console.WriteLine($"watch removed: {item.remove}, added: {item.add}");
        _watcherProcessQueue.Enqueue(item);
        if (Interlocked.CompareExchange(ref _watcherRunning, 1, 0) == 0)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessWatcherEvents();
                }
                finally
                {
                    Interlocked.Exchange(ref _watcherRunning, 0);
                }
            });
        }
    }
    
    private async Task ProcessWatcherEvents()
    {
        bool dosort = false;
        HashSet<string> addedPaths = new(StringComparer.OrdinalIgnoreCase);
        while (_watcherProcessQueue.TryDequeue(out var item))
        {
            var add = item.add;
            var remove = item.remove;
            var sort = item.sort;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (remove != null)
                {
                    var node = RootNode.FindNode(remove.LibraryNodeId);
                    if (_books.Remove(remove))
                    {
                        dosort = true;
                        if (node != null)
                        {
                            node.BooksCountChanged();
                            RemoveNodeIfEmpty(node);
                        }
                    }
                }

                if (add != null && _books.All(b => b.Path != add.Path))
                {
                    
                    var node = RootNode.FindNode(add.LibraryNodeId);
                    if (node is null)
                    {
                        //Create the node if we need to
                        node = RootNode.FindClosestNode(add.LibraryNodeId);
                        if (node is null) return;
                        node = AddNode (node, add.LibraryNodeId);
                        if (node is null) return;
                    }
                    _books.Add(add);
                    node.BooksCountChanged();
                    dosort = true;
                }

                if (sort) dosort = true;
            });
        }

        if (dosort)
        {
            await Dispatcher.UIThread.InvokeAsync(() => Sort(CurrentSortOption, CurrentSortAscending));
        }
    }
    public void RemoveEmptyBook(BookViewModel bookViewModel)
    {
        var idx = GetBookIndex(bookViewModel);
        if (idx == -1) return;
        _books.Remove(bookViewModel);
        Sort(CurrentSortOption, CurrentSortAscending);
    }
    #endregion





}

public class DummyItem
{
};