using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Bubbles4.Commands;
using Bubbles4.Models;
using Bubbles4.Services;
using CommunityToolkit.Mvvm.ComponentModel;
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

    public virtual async Task StartParsingLibraryAsync(string path)
    {
        // Cancel previous parsing run if active
        _parsingCts?.Cancel();
        _parsingCts?.Dispose();
        _parsingCts = new CancellationTokenSource();
        var token = _parsingCts.Token;
        var progress = new Progress<double>(p =>
        {
            Console.WriteLine($"Loading : {p:P1}");
        });
        Clear();

        try
        {
            await LibraryParserService.ParseLibraryRecursiveAsync(path, batch =>
            {
                // Marshal to UI thread
                Dispatcher.UIThread.Post(() =>
                {
                    AddBatch(batch);
                });
            }, cancellationToken: token, progress:progress);
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
        _books.AddRange(batch.Select(book => new BookViewModel(book, this, _mainViewModel)));
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

    public ICommand HandleItemPrepared => new AsyncRelayCommand<object>(async item =>
    {
        if (item is BookViewModel vm)
            await vm.PrepareThumbnailAsync();
    });

    public ICommand HandleItemClearing => new AsyncRelayCommand<object>(async item =>
    {
        if (item is BookViewModel vm)
            await vm.ClearThumbnailAsync();
    });

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

    int GetBookIndex(BookViewModel? book)
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

    public async Task OnDirectoryChanged(string path)
    {
        await Task.CompletedTask;
    }
    #endregion


}