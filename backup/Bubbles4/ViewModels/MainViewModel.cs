using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using Bubbles4.Models;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Bubbles4.Services;
using Bubbles4.Views;

namespace Bubbles4.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public AppStorage AppData { get; private set; }
    [ObservableProperty] private List<String> _libraries = new();
    
    
    private LibraryViewModel _library;
    public LibraryViewModel Library
    {
        get => _library;
        set
        {
            SetProperty(ref _library, value);
            if (value is LibraryNodeViewModel node)
            {
                if (node.Root == null && LibraryRoot != node)
                {
                    LibraryRoot = node;
                }
                if (node.Root != null && SelectedLibraryNode != node) SelectedLibraryNode = node;
            }
        } 
    }
    [ObservableProperty] private LibraryNodeViewModel? _libraryRoot;
    
    private LibraryNodeViewModel? _selectedLibraryNode;
    public LibraryNodeViewModel? SelectedLibraryNode
    {
        get => _selectedLibraryNode;
        set
        {
            SetProperty(ref _selectedLibraryNode, value);
            if(value != null && value != Library) Library = value;
        }
    }
    private bool _showNavPane;
    public bool ShowNavPane
    {
        //get => Library is LibraryNodeViewModel;
        get => _showNavPane;
        set => SetProperty( ref _showNavPane, value );
    }
    
    string? _libraryPath;
    public string? LibraryPath
    {
        get=>_libraryPath;
        set
        {
            SetProperty(ref _libraryPath, value);
            UpdateLibraryStatus();
        }
    }

    public SlidingImageCache _cache;
    
    [ObservableProperty] public BookViewModel? _selectedBook;
    [ObservableProperty] private ViewerData? _currentViewerData;
    
    private PageViewModel? _currentPageViewModel;
    public PageViewModel? CurrentPageViewModel
    {
        get => _currentPageViewModel;
        set => SetProperty(ref _currentPageViewModel, value);
    }
    
    private bool _isFullscreen;
    public bool IsFullscreen
    {
        get => _isFullscreen;
        set => SetProperty(ref _isFullscreen, value);   
    }

    public ICommand ToggleFullscreenCommand { get; }


    private readonly IDialogService _dialogService;
    
    private LibraryConfig? _config;
    private LibraryConfig _defaultConfig = new LibraryConfig();
    public LibraryConfig Config
    {
        get
        {
            return (_isFullscreen && _config != null)? _config : _defaultConfig;
        } 
        set
        {
            SetProperty(ref _config, value);
        }
    }

    private SortPreferences _sortPrefererences = new();
    
    //toolbar
    public SortPreferences.SortOptions[] SortOptions => Enum.GetValues<SortPreferences.SortOptions>();

    private SortPreferences.SortOptions _librarySortOption;
    public SortPreferences.SortOptions LibrarySortOption
    {
        get => _librarySortOption;
        set
        {
            SetProperty(ref _librarySortOption, value);
            Library.ChangeSort(value, LibrarySortDirection);
            _sortPrefererences.LibrarySortOption = value;
            _sortPrefererences.LibrarySortDirection = _librarySortDirection? 
                SortPreferences.SortDirection.Ascending : 
                SortPreferences.SortDirection.Descending;
        }
    }
    private bool _librarySortDirection;
    public bool LibrarySortDirection
    {
        get => _librarySortDirection;
        set
        {
            if(value != _librarySortDirection)Library.ReverseSortOrder();
            SetProperty(ref _librarySortDirection, value);
            _sortPrefererences.LibrarySortDirection = value? 
                SortPreferences.SortDirection.Ascending : 
                SortPreferences.SortDirection.Descending;
        } 
    }
    
    private SortPreferences.SortOptions _bookSortOption;
    public SortPreferences.SortOptions BookSortOption
    {
        get => _bookSortOption;
        set
        {
            SetProperty(ref _bookSortOption, value);
            if (SelectedBook != null)
                SelectedBook.ChangeSort(value, BookSortDirection);

            _sortPrefererences.BookSortOption = value;
            _sortPrefererences.BookSortDirection = _bookSortDirection?
                SortPreferences.SortDirection.Ascending :
                SortPreferences.SortDirection.Descending;
        }
    }
    private bool _bookSortDirection;
    public bool BookSortDirection
    {
        get => _bookSortDirection;
        set
        {
            if(value != _bookSortDirection && SelectedBook != null)
                SelectedBook.ReverseSortOrder();
            
            SetProperty(ref _bookSortDirection, value);
            _sortPrefererences.BookSortOption = _bookSortOption;
            _sortPrefererences.BookSortDirection = value?
                SortPreferences.SortDirection.Ascending :
                SortPreferences.SortDirection.Descending;
        }
    }
    
    //status bar
    [ObservableProperty] private string? _imageStatus;
    [ObservableProperty] private string? _pageNameStatus;
    [ObservableProperty] private string? _pageCreatedStatus;
    [ObservableProperty] private string? _bookStatus;
    [ObservableProperty] private string? _libraryStatus;
    [ObservableProperty] private string? _pagingStatus;
    public MainViewModel(IDialogService dialogService)
    {
        _dialogService = dialogService;
        _cache = new SlidingImageCache(this);
        Library = new LibraryViewModel(this, LibraryPath);
        ToggleFullscreenCommand = new RelayCommand(() =>
        {
            IsFullscreen = !IsFullscreen;
            var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow : null;
            (window as MainWindow)?.ToggleFullscreen();
            OnPropertyChanged(nameof(Config));
        });
        
        AppData = AppStorage.Load();
        Libraries = AppData.Libraries;
    }


    
    public async Task InitializeAsync(string? libraryPath)
    {
        
        await OpenLibrary(libraryPath);
    }

    public void OnClose()
    {
        if (!string.IsNullOrEmpty(LibraryPath) && _config != null)
        {
            _config.SortPreferences = _sortPrefererences;
            AppData.AddOrUpdate(LibraryPath, _config.Serialize());
            AppData.Save();    
        }
            
    }
    
    async Task OpenLibrary(string? libraryPath)
    {
        if (!string.IsNullOrEmpty(libraryPath) && Directory.Exists(libraryPath))
        {
            LibraryPath = libraryPath;
            string? json;
            AppData.Data.TryGetValue(LibraryPath, out json);

            if (!string.IsNullOrEmpty(json))
            {
                _config = LibraryConfig.Deserialize(json);
                OnPropertyChanged(nameof(Config));
            }
                

            if (_config != null)
            {
                _sortPrefererences = _config.SortPreferences;
                LibrarySortOption = _sortPrefererences.LibrarySortOption;
                LibrarySortDirection = _sortPrefererences.LibrarySortDirection == SortPreferences.SortDirection.Ascending;
                BookSortOption = _sortPrefererences.BookSortOption;
                BookSortDirection = _sortPrefererences.BookSortDirection == SortPreferences.SortDirection.Ascending;
            }

            if (_config == null || _config.IncludeSubdirectories)
            {
                Library = new LibraryViewModel(this, LibraryPath);
            }
            else
            {
                Library = new LibraryNodeViewModel(this, LibraryPath, null);
            }
            
            
            await Library.StartParsingLibraryAsync(LibraryPath);
        }
    }
    
    partial void OnCurrentViewerDataChanged(ViewerData? data)
    {
        UpdatePageNameStatus();
        UpdatePagingStatus();
        UpdateImageStatus();
    }
    
    [RelayCommand]
    private async Task PickDirectoryAsync()
    {
        var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (window != null)
        {
            var selectedPath = await _dialogService.PickDirectoryAsync(window);
            await OpenLibrary(selectedPath);
        }
    }

    [RelayCommand]
    public async Task ConfigureLibraryAsync()
    {
        var dialogVm = new LibraryConfigViewModel(new LibraryConfig());
        var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if (window != null)
        {
            var result = await _dialogService.ShowDialogAsync<LibraryConfig>(window, dialogVm);
            if (result != null)
            {
                Config = result;
            }    
        }
        
    }
    [RelayCommand] public void ExitFullScreenCommand()
    {
        IsFullscreen = false;
        var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow : null;
        (window as MainWindow)?.ExitFullscreen();
        OnPropertyChanged(nameof(Config));
    }


    #region input
    [RelayCommand]
    public async Task Next()
    {
        if (Library.Books.Count > 0)
        {
            //determine current book
            var book = Library.Books[0];
            if (SelectedBook != null) book = SelectedBook;
            bool more;
            do
            {
                //load book pages, 
                if (! book!.IsSelected)
                    book.IsSelected = true;
                
                if (book.LoadingTask != null)
                    //wait for the pages list loading task to complete.
                    await book.LoadingTask;
                
                //todo scroll to book in library view

                //select next page
                more = !book.NextPage();
                
                if(more == true) 
                {
                    // if next page doesn't exist in the selected book
                    Library.NextBook();
                    book = SelectedBook;
                }
            } while (more);
        }
    }
    [RelayCommand]
    public async Task Previous()
    {
        if (Library.Books.Count > 0)
        {
            //determine current book
            var book = Library.Books[Library.Books.Count - 1];
            if (SelectedBook != null) book = SelectedBook;
            bool more;
            do
            {
                //load book pages, 
                if (! book!.IsSelected)
                    book.IsSelected = true;
                
                if (book.LoadingTask != null)
                    //wait for the pages list loading task to complete.
                    await book.LoadingTask;
                
                //todo scroll to book in library view

                //select previous page
                more = !book.PreviousPage();
                
                if(more == true) 
                {
                    // if next page doesn't exist in the selected book
                    Library.PreviousBook();
                    book = SelectedBook;
                }
            } while (more);
        }
    }

    [RelayCommand]
    public async Task NextBook()
    {
        if (Library.Books.Count > 0)
        {
            var book = SelectedBook;
            if (book == null)
            {
                book = Library.Books[0];
                book.IsSelected = true;
            }
            else
            {
                Library.NextBook();
                book = SelectedBook;    
            }
            if (book!.LoadingTask != null)
                //wait for the pages list loading task to complete.
                await book.LoadingTask;
            
            await Next();
        }
        
    }

    [RelayCommand]
    public async Task PreviousBook()
    {

        if (Library.Books.Count > 0)
        {
            var book = SelectedBook;
            if (book == null)
            {
                book = Library.Books[Library.Books.Count - 1];
            }
            else
            {
                Library.PreviousBook();
                book = SelectedBook;    
            }
            if (book!.LoadingTask != null)
                //wait for the pages list loading task to complete.
                await book.LoadingTask;
            
            await Previous();
        }
        
    }
    #endregion
    
    
    

    

    #region status    
    //Status Bar
    public void UpdateLibraryStatus()
    {
        if(!string.IsNullOrEmpty(LibraryPath))
        {
            LibraryStatus = String.Format("Library Path : {0} | BookCount : {1}", LibraryPath, Library.Count);    
        }
        else LibraryStatus = "No Library Loaded"; 
    }
    public void UpdateBookStatus()
    {
        if (Library?.SelectedItem != null)
            BookStatus = Library.SelectedItem.Path;
        else BookStatus = "";
    }
    public void UpdatePageNameStatus()
    {
        if (Library.SelectedItem?.SelectedPage != null)
        {
            PageNameStatus = String.Format("{0} ", Library.SelectedItem.SelectedPage.Name);
            PageCreatedStatus = String.Format("Created : {0}", Library.SelectedItem.SelectedPage.Created);
        }
    }
    public void UpdateImageStatus()
    {
        if(CurrentViewerData != null)
            ImageStatus = String.Format("{0}px X {1}px", CurrentViewerData.Image.PixelSize.Width, CurrentViewerData.Image.PixelSize.Height);
        else ImageStatus = "";
    }

    public void UpdatePagingStatus()
    {
        if (SelectedBook == null || CurrentPageViewModel == null)
        {
            PagingStatus = "";
            return;
        }
        
        int idx = SelectedBook.GetPageIndex(CurrentPageViewModel)+1;
        int total = SelectedBook.PageCount;
        PagingStatus = String.Format("{0} / {1}", idx, total);
    }
    #endregion

}