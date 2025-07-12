using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
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
    protected List<BookViewModel> _books = new();
    protected ObservableCollection<BookViewModel> _booksMutable = new();
    public ReadOnlyObservableCollection<BookViewModel> Books { get; }
    [ObservableProperty] BookViewModel? _selectedItem;
    public LibraryConfig.SortOptions CurrentSortOption { get; set; }
    public bool CurrentSortAscending { get; set; }

    protected List<string>? _filters;
    public int Count => Books.Count;
    protected MainViewModel _mainViewModel;
    public event EventHandler<SelectedItemChangedEventArgs>? SelectionChanged;
    public event EventHandler? SortOrderChanged;
    public event EventHandler<int>? ScrollToIndexRequested;
    
    [ObservableProperty] private bool _isLoading;
    public LibraryViewModel(MainViewModel mainViewModel, string path)
    {
        _mainViewModel = mainViewModel;
        this.Path = path;
        Books = new ReadOnlyObservableCollection<BookViewModel>(_booksMutable);

        CurrentSortOption = _mainViewModel.Config?.LibrarySortOption ?? LibraryConfig.SortOptions.Path;
        var dir = _mainViewModel.Config?.LibrarySortAscending;
        CurrentSortAscending = dir ?? true;
    }

    public virtual void Close()
    {
        if(SelectedItem != null) 
            SelectedItem.IsSelected = false;
        
        SelectedItem = null;
        Clear();
    }
    public void Clear()
    {
        _books.Clear();
        _booksMutable.Clear();
        OnPropertyChanged(nameof(Count));
    }

    
    protected CancellationTokenSource? _parsingCts;

    public virtual async Task StartParsingLibraryAsync(string path, IProgress<(string, double, bool)> progress)
    {
        // Cancel previous parsing run if active
        _parsingCts?.Cancel();
        _parsingCts?.Dispose();
        _parsingCts = new CancellationTokenSource();
        var token = _parsingCts.Token;

        //Clear();

        try
        {
            Dispatcher.UIThread.Post(()=>IsLoading = true);
            await LibraryParserService.ParseLibraryRecursiveAsync(path, batch =>
            {
                // Marshal to UI thread
                AddBatch(batch);
            }, cancellationToken: token, progress: progress);
            Dispatcher.UIThread.Post(()=>IsLoading = false);
        }
        catch (OperationCanceledException)
        {
            // Optional: handle cancellation gracefully
        }

    }

    public void CancelParsing()
    {
        _parsingCts?.Cancel();
        _parsingCts?.Dispose();
        _parsingCts = null;
    }
    public async Task LoadSerializedCollection(string json, IProgress<(string, double, bool)> progress)
    {
        Dispatcher.UIThread.Post(()=>IsLoading = true);
        var strings = JsonSerializer.Deserialize<List<string>>(json);
        List<BookBase> bookbases = new();
        if (strings is not null)
        {
            var total = strings.Count;
            int i = 0;
            
            foreach (var item in strings)
            {
                var bookbase = BookBase.Deserialize(item);
                if(bookbase != null)
                    bookbases.Add(bookbase);
                
                progress.Report(($"Loading Cached Library Data...", (double)++i/total, false));
            }    
        }
        progress.Report(($"Loading Cached Library Data...", -1, false));
        await Dispatcher.UIThread.InvokeAsync(()=>AddBatch(bookbases, false));        
        progress.Report(($"Loading Cached Library Data...", 0, true));
    }

    public virtual void AddBatch(List<BookBase> batch, bool authoritative = true)
    {
        if (!Dispatcher.UIThread.CheckAccess())
            throw new InvalidOperationException("LibraryViewModel.AddBatch must be invoked on the UI thread.");

        if (!authoritative && _books.Count > 0)
            return;

        if (authoritative && _books.Count > 0)
        {
            var vmLookup = _books.ToDictionary(vm => vm.Path);
            var incomingPaths = new HashSet<string>(batch.Select(b => b.Path));
            int removed = 0, added = 0;
            // Remove any existing items not in the incoming batch
            for (int i = _books.Count - 1; i >= 0; i--)
            {
                if (!incomingPaths.Contains(_books[i].Path))
                {
                    _books.RemoveAt(i);
                    removed++;
                }
                    
            }
    
            // Add new ones not already in the VM list
            foreach (var newBook in batch)
            {
                if (!vmLookup.ContainsKey(newBook.Path))
                {
                    _books.Add(new BookViewModel(newBook, this, _mainViewModel));
                    added++;
                }
            }
            Console.WriteLine($"Authoritative set included. removes: {removed}, added: {added}");
            if (added > 0 || removed > 0)
            {
                Sort();
                _mainViewModel.UpdateLibraryStatus();
                OnPropertyChanged(nameof(Count));        
            }
        }
        else
        {
            _books.Clear();
            _books.AddRange(batch.Select(book => new BookViewModel(book, this, _mainViewModel)));
            Sort();
            _mainViewModel.UpdateLibraryStatus();
            OnPropertyChanged(nameof(Count));
        }

        
    }



    [RelayCommand]
    private async Task BookPrepared(object? parameter)
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
            await vm.PrepareThumbnailAsync();
        }
            
    }

    [RelayCommand]
    private async Task BookClearing(object? parameter)
    {
        if (parameter is BookViewModel vm)
        {
            //Console.WriteLine($"Clearing Thumbnail for book: {vm.Path}");
            await vm.ClearThumbnailAsync();    
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
        if (sort == null) sort = _mainViewModel.Config?.LibrarySortOption ?? LibraryConfig.SortOptions.Path;
        if (ascending == null) ascending = _mainViewModel.Config?.LibrarySortAscending ?? true;

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
        _mainViewModel.SelectedBook = value;
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
            //load book in bookview
            _ = Task.Run(async () =>
                {
                    try { await value.LoadingTask;}
                    catch(Exception x){Console.WriteLine(x);}                    
                    finally {value.LoadingTask = null;}
                });
        }
        InvokeSelectionChanged(SelectedItem, oldItem);
        _mainViewModel.UpdateBookStatus();
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

    #region FileSystem Watcher Events

    public virtual void FileSystemChanged(FileSystemEventArgs e)
    {
        bool sort = false;
        BookBase? newBook = null;
        BookViewModel? existing = null;
        BookViewModel? shouldRemove = null;
        BookViewModel? shouldAdd = null;
        bool removeFlag = false;
        
        bool isImage = FileTypes.IsImage(e.FullPath);
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
                    FileInfo info = new FileInfo(imgDir);
                    var bd = new BookDirectory(info.FullName, info.Name, -1,info.LastWriteTime, info.CreationTime);
                    //!! potentially creating many versions of the same bookdirectory, but they should be bounced off in the processing thread if it happens
                    shouldAdd = new BookViewModel(bd, this, _mainViewModel);
                }
            }
        }

        else
        {
            bool isArchive = FileTypes.IsArchive(e.FullPath);
            bool isPdf = FileTypes.IsPdf(e.FullPath);
            bool isDirbook = FileTypes.IsImageDirectory(e.FullPath);
   
            bool isBook = isDirbook || isPdf || isArchive;
            if (isBook) existing = _books.FirstOrDefault(x => string.Equals(x.Path, e.FullPath, StringComparison.OrdinalIgnoreCase));

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
                if (newBook != null) shouldAdd = new BookViewModel(newBook, this, _mainViewModel);
            }
        }
        if (shouldAdd != null || shouldRemove != null || sort)
        {
            EnqueueWatcherEvent((shouldAdd, shouldRemove, sort));
        }
    }

    private ConcurrentQueue<(BookViewModel? add, BookViewModel? remove, bool sort )> _watcherProcessQueue = new(); 
    
    private int _watcherRunning;

    public void EnqueueWatcherEvent((BookViewModel? add, BookViewModel? remove, bool sort) item)
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
    
    public async Task ProcessWatcherEvents()
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
                    _books.Remove(remove);
                    dosort = true;
                }

                if (add != null && addedPaths.Add(add.Path)) //idempotent enough?
                {
                    _books.Add(add);
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
    #endregion

    public string SerializeCollection()
    {
        List<string> list = new();
        foreach (var item in _books)
            list.Add(item.Model.Serialize());
        return JsonSerializer.Serialize(list);
    }


}

public class DummyItem
{
};