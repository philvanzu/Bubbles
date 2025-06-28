using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using Bubbles4.Models;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Bubbles4.Services;
using Bubbles4.Views;

namespace Bubbles4.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private AppStorage AppData { get; init; }
    public ObservableCollection<string> LibrariesList => AppData.LibrariesList;
    private LibraryViewModel _library;
    public LibraryViewModel Library
    {
        get => _library;
        set
        {
            SetProperty(ref _library, value);
            if (value is LibraryNodeViewModel node)
            {
                if (node.Parent == null && LibraryRoot != node) LibraryRoot = node;
                if (node.Parent != null && SelectedLibraryNode != node) SelectedLibraryNode = node;
            }
            else
            {
                LibraryRoot = null;
            }
            OnPropertyChanged(nameof(ShowLibraryTree));
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

    private bool _showNavPane;

    public bool ShowNavPane
    {
        get => _showNavPane;
        set
        {
            SetProperty(ref _showNavPane, value);
            if(value)
                OnPropertyChanged(nameof(LibrariesList));
            if (Config != null)
            {
                Config.ShowNavPane = value;
            }
        }
    }

    public bool ShowLibraryTree => LibraryRoot != null;
    
    
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
    
    private LibraryConfig? _config = new LibraryConfig("");

    public LibraryConfig? Config
    {
        get => _config;
        set
        {
            SetProperty(ref _config, value);
            if (value != null)
            {
                LibrarySortOption = value.LibrarySortOption;
                LibrarySortDirection = value.LibrarySortDirection == LibraryConfig.SortDirection.Ascending;
                BookSortOption = value.BookSortOption;
                BookSortDirection = value.BookSortDirection == LibraryConfig.SortDirection.Ascending;
                ShowNavPane = value.ShowNavPane;    
            }
        }
    }

    
    //toolbar
    public LibraryConfig.SortOptions[] SortOptions => Enum.GetValues<LibraryConfig.SortOptions>();

    private LibraryConfig.SortOptions _librarySortOption;
    public LibraryConfig.SortOptions LibrarySortOption
    {
        get => _librarySortOption;
        set
        {
            SetProperty(ref _librarySortOption, value);
            Library.ChangeSort(value, LibrarySortDirection);
            if (Config != null)
            {
                Config.LibrarySortOption = value;
                Config.LibrarySortDirection = _librarySortDirection? 
                    LibraryConfig.SortDirection.Ascending : 
                    LibraryConfig.SortDirection.Descending;    
            }
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
            if (Config != null)
            {
                Config.LibrarySortDirection = value? 
                    LibraryConfig.SortDirection.Ascending : 
                    LibraryConfig.SortDirection.Descending;    
            }
            
        } 
    }
    
    private LibraryConfig.SortOptions _bookSortOption;
    public LibraryConfig.SortOptions BookSortOption
    {
        get => _bookSortOption;
        set
        {
            SetProperty(ref _bookSortOption, value);
            if (SelectedBook != null)
                SelectedBook.ChangeSort(value, BookSortDirection);
            if (Config != null)
            {
                Config.BookSortOption = value;
                Config.BookSortDirection = _bookSortDirection?
                    LibraryConfig.SortDirection.Ascending :
                    LibraryConfig.SortDirection.Descending;    
            }
            
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
            if (Config != null)
            {
                Config.BookSortOption = _bookSortOption;
                Config.BookSortDirection = value?
                    LibraryConfig.SortDirection.Ascending :
                    LibraryConfig.SortDirection.Descending;    
            }
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
        _library = new LibraryViewModel(this, "dummy path");
        ToggleFullscreenCommand = new RelayCommand(() =>
        {
            IsFullscreen = !IsFullscreen;
            var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow : null;
            (window as MainWindow)?.ToggleFullscreen();
            OnPropertyChanged(nameof(Config));
        });
        AppData = AppStorage.Load();

    }


    
    public async Task InitializeAsync(string? libraryPath)
    {
        

            
        OnPropertyChanged(nameof(LibrariesList));            
        await OpenLibrary(libraryPath);
    }

    public void OnClose()
    {
        if(!string.IsNullOrEmpty(LibraryPath))
            CloseLibrary();
        else AppData.Save();
    }
    
    async Task OpenLibrary(string? libraryPath)
    {
        if(Library.Path != "")
            CloseLibrary();
        
        if (!string.IsNullOrEmpty(libraryPath) && Directory.Exists(libraryPath))
        {
            LibraryPath = libraryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            LibraryPath += Path.DirectorySeparatorChar;
            
            Config = AppData.GetConfig(libraryPath);
            
            bool nullconfig = Config == null;
            if(nullconfig) 
                Config  = new LibraryConfig(libraryPath);
                
            Library = Config!.Recursive ? 
                new LibraryViewModel(this, LibraryPath) : 
                new LibraryNodeViewModel(this, LibraryPath, Path.GetDirectoryName(LibraryPath)??"???_wtf_???");
            
            if (nullconfig)
            {
                AppData.AddOrUpdate(libraryPath, Config.Serialize());
                AppData.Save();
                OnPropertyChanged(nameof(LibrariesList));
            }

            OnPropertyChanged(nameof(Config));

            
            await Library.StartParsingLibraryAsync(LibraryPath);
            //Library.ChangeSort(LibrarySortOption, _librarySortDirection);
        }
    }

    public void CloseLibrary()
    {
        if (!string.IsNullOrEmpty(LibraryPath) && _config != null)
        {
            AppData.AddOrUpdate(LibraryPath, _config.Serialize());
            AppData.Save();
        }
    }

    partial void OnCurrentViewerDataChanged(ViewerData? value)
    {
        UpdatePageNameStatus();
        UpdatePagingStatus();
        UpdateImageStatus();
    }

    public void UpdateTreeView()
    {
        OnPropertyChanged(nameof(LibraryRoot));
        OnPropertyChanged(nameof(SelectedLibraryNode));
    }

    [RelayCommand]
    public async Task CreateLibraryAsync()
    {
        
        var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (window != null)
        {
            string? selectedPath = await _dialogService.PickDirectoryAsync(window);

            if (!string.IsNullOrEmpty(selectedPath) && Directory.Exists(selectedPath))
            {
                selectedPath = selectedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                selectedPath += Path.DirectorySeparatorChar;
            
                var dialogVm = new LibraryConfigViewModel(new LibraryConfig(selectedPath));
                var result = await _dialogService.ShowDialogAsync<LibraryConfig>(window, dialogVm);
                if (result != null)
                {
                    AppData.AddOrUpdate(result.Path, result.Serialize());
                    AppData.Save();
                    OnPropertyChanged(nameof(LibrariesList));     
                }    
                await OpenLibrary(selectedPath);
            
            }
        }
            
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
        if (!string.IsNullOrEmpty(LibraryPath))
        {
            var config  = AppData.GetConfig(LibraryPath);
            if (config == null) config = Config;
            if (config == null) config = new LibraryConfig(LibraryPath);
            var dialogVm = new LibraryConfigViewModel(config);
            var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            if (window != null)
            {
                var result = await _dialogService.ShowDialogAsync<LibraryConfig>(window, dialogVm);
                if (result != null)
                {
                    Config = result;
                    AppData.AddOrUpdate(Config.Path, Config.Serialize());
                    AppData.Save();
                }    
            }
        }
    }
    [RelayCommand]
    public void OnOpenLibraryPressed(string? path)
    {
        Task.Run(()=>OpenLibrary(path))
            .ContinueWith(t =>
            {
                if (t.IsFaulted) Console.WriteLine(t.Exception.ToString());
            });
    }

    [RelayCommand]
    public async Task OnDeleteLibraryPressed(string? path)
    {
        if (path == LibraryPath)
        {
            
        }
        else
        {
            var dialog = new OkCancelViewModel
            {
                Content = $"Do you want to delete the recorded setting for the library at [{path}] ?"
            };
            var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            if (window != null)
            {
                var result = await _dialogService.ShowDialogAsync<bool>(window, dialog);
                if (result)
                {
                    // proceed
                }    
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
                
                if(more) 
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
                
                if(more) 
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
        if (Library.SelectedItem != null)
            BookStatus = Library.SelectedItem.Name;
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
            ImageStatus = String.Format("{0}px X {1}px", CurrentViewerData?.Image?.PixelSize.Width, CurrentViewerData?.Image?.PixelSize.Height);
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