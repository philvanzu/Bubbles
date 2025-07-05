using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Bubbles4.Models;
using Bubbles4.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;

namespace Bubbles4.ViewModels;

public partial class LibraryViewModel: ViewModelBase
{
    public string Path { get; }
    protected List<BookViewModel> _books = new();
    protected ObservableCollection<BookViewModel> _booksMutable = new();
    public ReadOnlyObservableCollection<BookViewModel> Books { get; }
    protected LibraryConfig.SortOptions _currentSortOption;
    protected bool _currentSortAscending;
    protected List<string>? _filters;
    public int Count => Books.Count;
    protected MainViewModel _mainViewModel;
    
    public LibraryViewModel(MainViewModel mainViewModel, string path)
    {
        _mainViewModel = mainViewModel;
        this.Path = path;
        Books = new ReadOnlyObservableCollection<BookViewModel>(_booksMutable);

        _currentSortOption = _mainViewModel.Config?.LibrarySortOption ?? LibraryConfig.SortOptions.Path;
        var dir = _mainViewModel.Config?.LibrarySortAscending;
        _currentSortAscending = dir ?? true;
    }
    
    public void Clear()
    {
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

        Clear();

        try
        {
            await LibraryParserService.ParseLibraryRecursiveAsync(path, batch =>
            {
                // Marshal to UI thread
                AddBatch(batch);
            }, cancellationToken: token, progress: progress);
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
    
    public virtual void AddBatch(List<BookBase> batch)
    {        
        if (!Dispatcher.UIThread.CheckAccess())
            throw(new InvalidOperationException("LibraryViewModel.AddBatch Must be invoked on the UI thread."));

        _books.AddRange(batch.Select(book => new BookViewModel(book, this, _mainViewModel)));
        Sort();
        _mainViewModel.UpdateLibraryStatus();
        OnPropertyChanged(nameof(Count));
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

            LibraryConfig.SortOptions.Random => RandomizeAndReturnComparer(),

            _ => SortExpressionComparer<BookViewModel>.Ascending(x => x.Name)
        };
    }
    private IComparer<BookViewModel> RandomizeAndReturnComparer()
    {
        int seed = CryptoRandom.NextInt();

        int Hash(string input)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in input)
                    hash = hash * 31 + c;
                return hash;
            }
        }

        return Comparer<BookViewModel>.Create((x, y) =>
        {
            int xHash = Hash(x.Path) ^ seed;
            int yHash = Hash(y.Path) ^ seed;
            return xHash.CompareTo(yHash);
        });
    }

    public void Sort(LibraryConfig.SortOptions? sort=null, bool? ascending=null)
    {
        if (sort == null) sort = _mainViewModel.Config?.LibrarySortOption ?? LibraryConfig.SortOptions.Path;
        if (ascending == null) ascending = _mainViewModel.Config?.LibrarySortAscending ?? true;

        _currentSortOption = sort.Value;
        _currentSortAscending = ascending.Value;
        ApplyFilterAndSort();
    }
    public void Filter(List<string>? keywords = null)
    {
        _filters = keywords;
        ApplyFilterAndSort();
    }
    private void ApplyFilterAndSort()
    {
        var comparer = GetComparer(_currentSortOption, _currentSortAscending);

        var filtered = _books
            .Where(b => MatchesKeywords(b, _filters))
            .OrderBy(b => b, comparer);

        _booksMutable.Clear();
        _booksMutable.AddRange(filtered);

        OnPropertyChanged(nameof(Books));
    }
    private bool MatchesKeywords(BookViewModel bvm, List<string>? keywords)
    {
        if (keywords == null || keywords.Count == 0)
            return true;

        foreach (var keyword in keywords)
        {
            if (bvm.Path.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    public void ReverseSortOrder()
    {
        _currentSortAscending = !_currentSortAscending;
        Sort(_currentSortOption, _currentSortAscending);
    }
    [RelayCommand]
    private async Task BookPrepared(object? parameter)
    {
        if (parameter is BookViewModel vm)
        {
            if(GetBookIndex(vm) == 0)
                Console.WriteLine($"item {GetBookIndex(vm)} prepared.");
            await vm.PrepareThumbnailAsync();
        }
            
    }

    [RelayCommand]
    private async Task BookClearing(object? parameter)
    {
        if (parameter is BookViewModel vm)
        {
            var idx = GetBookIndex(vm);
            if (idx == 0)
                Console.WriteLine($"item {GetBookIndex(vm)} clearing");
            await vm.ClearThumbnailAsync();    
        }
            
    }
    

    [ObservableProperty] BookViewModel? _selectedItem;



    partial void OnSelectedItemChanged(BookViewModel? value)
    {
        _mainViewModel.SelectedBook = value;
        foreach (var book in Books)
        {
            if (book != value && book.IsSelected)
            {
                //unload book in BookView and dispose pages data
                book.UnloadPagesList();
                book.IsSelected = false;
            }
        }
        if (value != null)
        {
            //load book in bookview
            value.LoadingTask = value.LoadPagesListAsync()
                .ContinueWith(_ => value.LoadingTask = null
                , TaskScheduler.FromCurrentSynchronizationContext());
        }
        _mainViewModel.UpdateBookStatus();
        //Console.WriteLine("Selected book :" + value.Name );
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
        Books[index].IsSelected = true;
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
        Books[index].IsSelected = true;
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
                if(bvm?.Pages.Count > 0) bvm?.PageFileChanged(e);
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

    public virtual void FileSystemRenamed(RenamedEventArgs e)
    {
        bool sort = false;
        BookViewModel? shouldRemove = null;
        BookViewModel? shouldAdd = null;

        if (FileTypes.IsImage(e.FullPath))
        {
            string? imgDir = System.IO.Path.GetDirectoryName(e.FullPath);
            string? oldImgDir = System.IO.Path.GetDirectoryName(e.OldFullPath);
            if (imgDir is not null)
            {
                var bvm = _books.FirstOrDefault(x => string.Equals(x.Path, imgDir, StringComparison.OrdinalIgnoreCase));
                if (bvm?.Pages.Count > 0) bvm.PageFileRenamed(e);
            }
            if (oldImgDir is not null)
            {
                var bvmOld = _books.FirstOrDefault(x => string.Equals(x.Path, oldImgDir, StringComparison.OrdinalIgnoreCase));
                bvmOld?.PageFileRenamed(e); // Let it interpret that it lost a file
            }
        }
        else
        {
            // Check whether the old path was a book
            bool wasBook = FileTypes.IsArchive(e.OldFullPath) ||
                           FileTypes.IsPdf(e.OldFullPath) ||
                           FileTypes.IsImageDirectory(e.OldFullPath);

            // Check whether the new path is a book
            bool isNowBook = FileTypes.IsArchive(e.FullPath) ||
                             FileTypes.IsPdf(e.FullPath) ||
                             FileTypes.IsImageDirectory(e.FullPath);

            // Remove the old book if it existed
            if (wasBook)
            {
                var oldEntry = _books.FirstOrDefault(x =>
                    string.Equals(x.Path, e.OldFullPath, StringComparison.OrdinalIgnoreCase));
                if (oldEntry != null)
                {
                    if (isNowBook)
                        oldEntry.FileSystemRenamed(e);
                    else shouldRemove = oldEntry;
                    sort = true;
                }
            }
            // Add the new book if it qualifies
            else if (isNowBook)
            {
                BookBase? newBook = null;

                if (FileTypes.IsArchive(e.FullPath))
                {
                    var info = new FileInfo(e.FullPath);
                    newBook = new BookArchive(info.FullName, info.Name, -1, info.LastWriteTime, info.CreationTime);
                }
                else if (FileTypes.IsPdf(e.FullPath))
                {
                    var info = new FileInfo(e.FullPath);
                    newBook = new BookPdf(info.FullName, info.Name, -1, info.LastWriteTime, info.CreationTime);
                }
                else if (FileTypes.IsImageDirectory(e.FullPath))
                {
                    var info = new DirectoryInfo(e.FullPath);
                    newBook = new BookDirectory(info.FullName, info.Name, -1, info.LastWriteTime, info.CreationTime);
                }

                if (newBook != null)
                {
                    var bvm = new BookViewModel(newBook, this, _mainViewModel);
                    shouldAdd = bvm;
                }
                sort = true;
            }

            if (shouldAdd != null || shouldRemove != null || sort)
            {
                EnqueueWatcherEvent((shouldAdd, shouldRemove, sort));
            }    
        }
    }

    private ConcurrentQueue<(BookViewModel? add, BookViewModel? remove, bool sort )> _watcherProcessQueue = new(); 
    
    private int _watcherRunning = 0;

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
            await Dispatcher.UIThread.InvokeAsync(() => Sort(_currentSortOption, _currentSortAscending));
        }
    }
    #endregion


}

public class DummyItem
{
};