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
using Bubbles4.Commands;
using Bubbles4.Models;
using Bubbles4.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using DynamicData.Binding;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Linq;

namespace Bubbles4.ViewModels;

public partial class LibraryViewModel: ViewModelBase
{
    public string Path { get; set; }
    private ReadOnlyObservableCollection<BookViewModel> _books;

    public ReadOnlyObservableCollection<BookViewModel> Books
    {
        get => _books;
        set
        {
            if (SetProperty(ref _books, value))
            {
                OnPropertyChanged(nameof(Count));
            }
        }
    }
    private readonly SourceList<BookViewModel> _bookSource = new();
    private IDisposable? _booksConnection;
    /*
    private SortExpressionComparer<BookViewModel> _currentSort
        = SortExpressionComparer<BookViewModel>.Ascending(x => x.Name);
    */
    private SortPreferences.SortOptions _currentSortOption = SortPreferences.SortOptions.Path;
    private bool _currentSortDirection = true;
    public int Count => Books.Count;
    protected MainViewModel _mainViewModel;
    public LibraryViewModel(MainViewModel mainViewModel, string path)
    {
        _mainViewModel = mainViewModel;
        this.Path = path;
        _books = new ReadOnlyObservableCollection<BookViewModel>(new ObservableCollection<BookViewModel>());
        
        
        _booksConnection = _bookSource
            .Connect()
            //.Sort(SortExpressionComparer<BookViewModel>.Ascending(x => x.Name))
            .Bind(out _books)
            .AutoRefreshOnObservable(_ => Observable.Return(Unit.Default)) // Optional: can use to refresh view
            .Subscribe();
        
        
        
    }
    public void Clear()
    {
        _bookSource.Clear();
        OnPropertyChanged(nameof(Count));
    }

    
    private CancellationTokenSource? _parsingCts;

    public async Task StartParsingLibraryAsync(string path)
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
                Dispatcher.UIThread.Post(() =>
                {
                    AddBatch(batch);
                });
            }, cancellationToken: token);
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
    public void AddBatch(List<BookBase> batch)
    {
        foreach (var book in batch)
        {
            _bookSource.Add(new BookViewModel(book, this, _mainViewModel));
            OnPropertyChanged(nameof(Count));
            _mainViewModel.UpdateLibraryStatus();
            //Console.WriteLine(book.Name + " has been added to the library");
        }
    }
    public void ChangeSort(SortPreferences.SortOptions sort, bool direction)
    {
        // Dispose the previous connection
        _booksConnection?.Dispose();

        // Rebuild the pipeline with the new sort
        var conn = _bookSource.Connect();
        IObservable<IChangeSet<BookViewModel>> sorted=null;
        switch (sort)
        {
            case SortPreferences.SortOptions.Path:
                sorted = (direction) ? 
                    conn.Sort(SortExpressionComparer<BookViewModel>.Ascending(x => x.Path)):
                    conn.Sort(SortExpressionComparer<BookViewModel>.Descending(x => x.Path));
                break;
            case SortPreferences.SortOptions.Alpha:
                sorted = (direction) ? 
                    conn.Sort(SortExpressionComparer<BookViewModel>.Ascending(x => x.Name)):
                    conn.Sort(SortExpressionComparer<BookViewModel>.Descending(x => x.Name));
                break;
            case SortPreferences.SortOptions.Created:
                sorted = (direction) ? 
                    conn.Sort(SortExpressionComparer<BookViewModel>.Ascending(x => x.Created)):
                    conn.Sort(SortExpressionComparer<BookViewModel>.Descending(x => x.Created));
                break;
            case SortPreferences.SortOptions.Modified:
                sorted = (direction) ? 
                    conn.Sort(SortExpressionComparer<BookViewModel>.Ascending(x => x.LastModified)):
                    conn.Sort(SortExpressionComparer<BookViewModel>.Descending(x => x.LastModified));
                break;
            case SortPreferences.SortOptions.Natural:
                sorted = conn.Sort(new BookViewModelNaturalComparer(direction));
                break;
            
            case SortPreferences.SortOptions.Random:
                var rng = new Random();
                foreach (var book in _bookSource.Items)
                    book.RandomIndex = CryptoRandom.NextInt();
                // Use identity sort or no sort (or a no-op comparer)
                sorted = conn.Sort(SortExpressionComparer<BookViewModel>.Ascending(x => x.RandomIndex));
                break;
        }

        _booksConnection = 
            sorted!.Bind(out _books)
            .Subscribe();

        OnPropertyChanged(nameof(Books));
        _currentSortOption = sort;
        _currentSortDirection = direction;
    }

    public void ReverseSortOrder()
    {
        _currentSortDirection = !_currentSortDirection;
        ChangeSort(_currentSortOption, _currentSortDirection);
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
                .ContinueWith(t => value.LoadingTask = null
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

}