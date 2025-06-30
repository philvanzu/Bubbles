using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Linq;
using Bubbles4.Models;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Bubbles4.Services;
using Bubbles4.Views;

namespace Bubbles4.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private AppStorage AppData { get; init; }
    public ObservableCollection<string> LibrariesList => AppData.LibrariesList;
    private LibraryViewModel? _library;
    public LibraryViewModel? Library
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
    public IDialogService DialogService => _dialogService;
    
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
                LibrarySortAscending = value.LibrarySortAscending;
                BookSortOption = value.BookSortOption;
                BookSortAscending = value.BookSortAscending;
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
            Library?.Sort(value, LibrarySortAscending);
            if (Config != null)
            {
                Config.LibrarySortOption = value;
                Config.LibrarySortAscending = _librarySortAscending;    
            }
        }
    }
    private bool _librarySortAscending;
    public bool LibrarySortAscending
    {
        get => _librarySortAscending;
        set
        {
            if(value != _librarySortAscending)Library?.ReverseSortOrder();
            SetProperty(ref _librarySortAscending, value);
            if (Config != null) Config.LibrarySortAscending = value;    
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
                SelectedBook.Sort(value, BookSortAscending);
            if (Config != null)
            {
                Config.BookSortOption = value;
                Config.BookSortAscending = _bookSortAscending;    
            }
            
        }
    }
    private bool _bookSortAscending;
    public bool BookSortAscending
    {
        get => _bookSortAscending;
        set
        {
            if(value != _bookSortAscending && SelectedBook != null)
                SelectedBook.ReverseSortOrder();
            
            SetProperty(ref _bookSortAscending, value);
            if (Config != null)
            {
                Config.BookSortOption = _bookSortOption;
                Config.BookSortAscending = value;
            }
        }
    }

    [ObservableProperty] private string _searchString = string.Empty;
    
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

    public void InitializeAsync(string? libraryPath)
    {
        if (libraryPath != null)
            OpenLibrary(libraryPath);
    }

    public void OnClose()
    {
        if(!string.IsNullOrEmpty(Library?.Path))
            CloseLibrary();
        else AppData.Save();
    }
    
    void OpenLibrary(string? libraryPath)
    {
        if(Library != null)
            CloseLibrary();
        
        if (!string.IsNullOrEmpty(libraryPath) && Directory.Exists(libraryPath))
        {
            libraryPath = libraryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            libraryPath += Path.DirectorySeparatorChar;
            
            var config = AppData.GetConfig(libraryPath);
            
            if(config == null) 
                config  = new LibraryConfig(libraryPath);

            var libraryName = Path.GetFileName(Path.GetDirectoryName(libraryPath.TrimEnd(Path.DirectorySeparatorChar))) ?? libraryPath;
            Library = config.Recursive ? 
                new LibraryViewModel(this, libraryPath) : 
                new LibraryNodeViewModel(this, libraryPath, libraryName);

            Config = config;            
            AppData.AddOrUpdate(libraryPath, config.Serialize());
            AppData.Save();
            OnPropertyChanged(nameof(LibrariesList));
//            OnPropertyChanged(nameof(Config));

            _ = Task.Run(()=> Library.StartParsingLibraryAsync(libraryPath))
                .ContinueWith((t) =>
                {
                    if (t.IsFaulted) { Console.WriteLine(t.Exception); }
                    else Library.Sort(LibrarySortOption, LibrarySortAscending); 
                });
        }
    }

    public void CloseLibrary()
    {
        if (Library != null)
        {
            
            OnPropertyChanged(nameof(CurrentViewerData));
            if (_config != null)
            {
                var save = Library;
                if (Library is LibraryNodeViewModel library) save = library.Root;
                AppData.AddOrUpdate(save.Path, _config.Serialize());
                AppData.Save();    
            }
            Library.Clear();
        }    
    }

    partial void OnCurrentViewerDataChanged(ViewerData? value)
    {
        _ = value;
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
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (result != null)
                    {
                        AppData.AddOrUpdate(result.Path, result.Serialize());
                        AppData.Save();
                        OnPropertyChanged(nameof(LibrariesList)); 
                    }    
                    OpenLibrary(selectedPath);
                });
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
            await Dispatcher.UIThread.InvokeAsync(() => OpenLibrary(selectedPath));
        }
    }

    [RelayCommand]
    public async Task ConfigureLibraryAsync()
    {
        if (Library!= null)
        {
            var config  = AppData.GetConfig(Library.Path);
            if (config == null) config = Config;
            if (config == null) config = new LibraryConfig(Library.Path);
            var dialogVm = new LibraryConfigViewModel(config);
            var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            if (window != null)
            {
                var result = await _dialogService.ShowDialogAsync<LibraryConfig>(window, dialogVm);
                if (result != null)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Config = result;
                        AppData.AddOrUpdate(Config.Path, Config.Serialize());
                        AppData.Save();
                    });
                }    
            }
        }
    }
    [RelayCommand]
    public void OnOpenLibraryPressed(string? path)
    {
        try
        {
            OpenLibrary(path);    
        }
        catch (Exception ex){Console.WriteLine(ex);}
            
    }

    [RelayCommand]
    public async Task OnDeleteLibraryPressed(string? path)
    {
        if (path == null) return;
        
        if (path == Library?.Path)
        {
            
        }
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
                AppData.Remove(path);
                AppData.Save();
                OnPropertyChanged(nameof(LibrariesList));
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

    [RelayCommand]
    private void Search(string keywords)
    {
        // You can parse keywords here or pass them directly to your filtering method
        var keywordList = keywords
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        Library?.Filter(keywordList);
    }
    [RelayCommand]
    private void ClearSearch()
    {
        SearchString = string.Empty;
        Search("");
    }
    

    #region input
    [RelayCommand]
    public async Task Next()
    {
        if (Library == null) return;
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
        if(Library == null) return; 
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
        if(Library == null) return; 
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
        if(Library == null) return; 
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

    [RelayCommand]
    private void FirstPage()
    {
        if (Library == null || SelectedBook==null) return;
        SelectedBook.FirstPage();
    }

    [RelayCommand]
    private void LastPage()
    {
        if (Library == null || SelectedBook == null) return;
        SelectedBook.LastPage();
    }
    
    #endregion

    #region status    
    //Status Bar
    public void UpdateLibraryStatus()
    {
        if(Library != null)
        {
            LibraryStatus = String.Format("Library Path : {0} | BookCount : {1}", Library.Path, Library.Count);    
        }
        else LibraryStatus = "No Library Loaded"; 
    }
    public void UpdateBookStatus()
    {
        if (Library?.SelectedItem != null)
            BookStatus = Library.SelectedItem.Name;
        else BookStatus = "";
    }
    public void UpdatePageNameStatus()
    {
        if (Library?.SelectedItem?.SelectedPage != null)
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