using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Bubbles4.Models;
using Bubbles4.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;

namespace Bubbles4.ViewModels;

public partial class BookViewModel: ViewModelBase
{
    private LibraryViewModel _library;
    private BookBase _book;
    public BookBase Model => _book;
    
    public string? Path => _book.Path;
    public string Name => _book.Name;
    public int PageCount => _book.PageCount;
    public DateTime LastModified => _book.LastModified;
    public DateTime Created => _book.Created;
    public int RandomIndex;
    [ObservableProperty] private IvpCollection? _imageViewingParamsCollection;

    MainViewModel _mainViewModel;
    public MainViewModel MainViewModel => _mainViewModel;
    Bitmap? _thumbnail;
    public Bitmap? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value);
    }
    private bool _isThumbnailLoading;
    public Task? LoadingTask { get; set; }
    
    ReadOnlyObservableCollection<PageViewModel> _pages;
    public ReadOnlyObservableCollection<PageViewModel> Pages
    {
        get => _pages;
        set => SetProperty(ref _pages, value); 
    } 
    
    private readonly SourceList<PageViewModel> _pageSource = new();
    private IDisposable? _pagesConnection;
    private LibraryConfig.SortOptions _currentSortOption = LibraryConfig.SortOptions.Natural;
    private bool _currentSortDirection = true; //ascending
    
    public BookViewModel(BookBase book, LibraryViewModel library, MainViewModel mainViewModel)
    {
        this._mainViewModel = mainViewModel;
        this._book = book;
        this._library = library;
        
        _pages = new ReadOnlyObservableCollection<PageViewModel>(new ObservableCollection<PageViewModel>());
        
        
        _pagesConnection = _pageSource
        .Connect()
        //.Sort(SortExpressionComparer<BookViewModel>.Ascending(x => x.Name))
        .Bind(out _pages)
        .AutoRefreshOnObservable(_ => Observable.Return(Unit.Default)) // Optional: can use to refresh view
        .Subscribe();

    }
    
    
    public void ChangeSort(LibraryConfig.SortOptions sort, bool direction)
    {
        // Dispose the previous connection
        _pagesConnection?.Dispose();

        // Rebuild the pipeline with the new sort
        var conn = _pageSource.Connect();
        IObservable<IChangeSet<PageViewModel>>? sorted=null;
        switch (sort)
        {
            case LibraryConfig.SortOptions.Path:
                sorted = (direction) ? 
                    conn.Sort(SortExpressionComparer<PageViewModel>.Ascending(x => x.Path)):
                    conn.Sort(SortExpressionComparer<PageViewModel>.Descending(x => x.Path));
                break;
            case LibraryConfig.SortOptions.Alpha:
                sorted = (direction) ? 
                    conn.Sort(SortExpressionComparer<PageViewModel>.Ascending(x => x.Name)):
                    conn.Sort(SortExpressionComparer<PageViewModel>.Descending(x => x.Name));
                break;
            case LibraryConfig.SortOptions.Created:
                sorted = (direction) ? 
                    conn.Sort(SortExpressionComparer<PageViewModel>.Ascending(x => x.Created)):
                    conn.Sort(SortExpressionComparer<PageViewModel>.Descending(x => x.Created));
                break;
            case LibraryConfig.SortOptions.Modified:
                sorted = (direction) ? 
                    conn.Sort(SortExpressionComparer<PageViewModel>.Ascending(x => x.LastModified)):
                    conn.Sort(SortExpressionComparer<PageViewModel>.Descending(x => x.LastModified));
                break;
            case LibraryConfig.SortOptions.Natural:
                sorted = conn.Sort(new PageViewModelNaturalComparer(direction));
                break;
            
            case LibraryConfig.SortOptions.Random:
                foreach (var page in _pageSource.Items)
                    page.RandomIndex = CryptoRandom.NextInt();
                // Use identity sort or no sort (or a no-op comparer)
                sorted = conn.Sort(SortExpressionComparer<PageViewModel>.Ascending(x => x.RandomIndex));
                break;
        }

        _pagesConnection = 
            sorted!.Bind(out _pages)
            .Subscribe();

        OnPropertyChanged(nameof(Pages));
        _currentSortOption = sort;
        _currentSortDirection = direction;
    }
    
    public void ReverseSortOrder()
    {
        _currentSortDirection = !_currentSortDirection;
        ChangeSort(_currentSortOption, _currentSortDirection);
    }
    
    public async Task PrepareThumbnailAsync()
    {   
        if (_thumbnail != null || _isThumbnailLoading)
            return;

        _isThumbnailLoading = true;
        try
        {
            //Console.WriteLine($"awaiting thumbnail from BookViewModel {Path}");
            await _book.LoadThumbnailAsync(bitmap => Thumbnail = bitmap );
            
            if (_book is BookArchive)
            {
                if (Thumbnail == null)
                {
                    //Console.WriteLine("null bitmap returned for book thumbnail : "+ Path);
                }
                else 
                {
                    //Console.WriteLine("Thumbnail received for {0} : {1}px X {2}px", Path, Thumbnail.PixelSize.Width, Thumbnail.PixelSize.Height);
                }    
            }
        }
        catch (Exception ex){Console.WriteLine(ex);}
        finally
        {
            _isThumbnailLoading = false;
        }
    }
    public async Task ClearThumbnailAsync()
    {
        //Console.WriteLine("unloading thmbnail :" +this.Name);
        if (_isThumbnailLoading) _book.CancelThumbnailLoad();
        _thumbnail?.Dispose();
        _thumbnail = null;
        
        OnPropertyChanged(nameof(Thumbnail));
        await Task.CompletedTask;
    }
    public async Task PreparePageThumbnailAsync(PageViewModel page)
    {
        if (page.Thumbnail != null || page.IsThumbnailLoading || string.IsNullOrEmpty(page.Path))
            return;
        page.IsThumbnailLoading = true;
        try
        {
            await _book.LoadThumbnailAsync(bitmap => page.Thumbnail = bitmap, page.Path);

        }
        finally
        {
            page.IsThumbnailLoading = false;
        }
    }
    
    public async Task LoadPagesListAsync()
    {
        var list = new List<PageViewModel>();
        await _book.LoadPagesList(pgs =>
        {
            if (pgs != null)
                foreach (var page in pgs)
                {
                    list.Add(new PageViewModel(this, page));
                    _book.PagesCts.TryAdd(page.Path, null);
                }
        });
        _pageSource.Clear();
        foreach( var p in list)
            _pageSource.Add(p);
            
        OnPropertyChanged(nameof(Pages));
        ImageViewingParamsCollection = IvpCollection.Load(_book.IvpPath);
    }

    public void UnloadPagesList()
    {
        _book.CancelPagesListLoad();
        if (ImageViewingParamsCollection != null)
        {
            
            //ImageViewingParamsCollection.Save(_book.IvpPath);
            ImageViewingParamsCollection = null;
        }
        foreach (var page in Pages) page.Unload();
        if(Pages.Count > 0)_pageSource.Clear();
        
        if (_book.PagesCts.Count > 0)
        {
            foreach (var kv in _book.PagesCts)
            {
                if (kv.Value != null)
                {
                    kv.Value.Cancel();
                    kv.Value.Dispose();
                }
            }
            _book.PagesCts.Clear();
        }
        
        SelectedPage = null;
    }

    [RelayCommand]
    public async Task OnSelection()
    {
        IsSelected = true;
        if(SelectedPage != null) SelectedPage.IsSelected = false;
        await _mainViewModel.Next();
    }
    [RelayCommand]
    private void StartRenamingCommand(){}
    
    [RelayCommand]
    private void DeleteCommand(){}
    [RelayCommand]
    private void OpenInExplorerCommand(){}
    [RelayCommand]
    private void OpenFileCommand(){}
    [RelayCommand]
    private void ShowDetailsCommand(){}

    [ObservableProperty] bool _isSelected;
    partial void OnIsSelectedChanged(bool oldValue, bool newValue)
    {
        if (newValue && _library.SelectedItem != this)
        {
            _library.SelectedItem = this;
        }
        else if (oldValue && _library.SelectedItem == this)
        {
            _library.SelectedItem = null;
        }
    }

    public int GetPageIndex(PageViewModel pageViewModel)
    {
        return Pages.IndexOf(pageViewModel);
    }
    public ICommand HandleItemPrepared => new AsyncRelayCommand<object>(
        async item =>
        {
            if (item is PageViewModel vm)
            {
                //Console.WriteLine($"preparing item {vm.Path}");
                await vm.LoadThumbnailAsync();
            }
            //else Console.WriteLine("Not a page");
        }
        ,
        item => item is PageViewModel, 
        options: AsyncRelayCommandOptions.AllowConcurrentExecutions
    );

    public ICommand HandleItemClearing => new AsyncRelayCommand<object>(async item =>
    {
        if (item is PageViewModel vm)
        {
            await vm.UnLoadAsync();
            //Console.WriteLine("Unloading page :"+vm.Name);
        }
            
    });
    
    [ObservableProperty] PageViewModel? _selectedPage;

    //private CancellationTokenSource? _imgLoadCts;
    partial void OnSelectedPageChanged(PageViewModel? value)
    {
        foreach (var page in Pages)
        {
            if (page != value && page.IsSelected)
            {
                //unload page in PageView and dispose pages data
                page.IsSelected = false;
            }
        }
        _mainViewModel.CurrentPageViewModel = value;

        _mainViewModel.UpdatePageNameStatus();
    }


    public bool NextPage()
    {
        int index = -1;
        if(SelectedPage != null) index = GetPageIndex(SelectedPage);
        
        if (Pages.Count <= 0) return false;
        if (index == -1) index = 0;
        else
        {
            int newIndex = index + 1;
            if (newIndex >= Pages.Count) return false;
            index = newIndex;
        }
        Pages[index].IsSelected = true;
        return true;
    }

    public bool PreviousPage()
    {
        int index = Pages.Count;
        if(SelectedPage != null ) index = GetPageIndex(SelectedPage);
        if (Pages.Count <= 0) return false;
        else
        {
            int newIndex = index - 1;
            if (newIndex < 0) return false;
            index = newIndex;
        }
        Pages[index].IsSelected = true;
        return true;
    }





}