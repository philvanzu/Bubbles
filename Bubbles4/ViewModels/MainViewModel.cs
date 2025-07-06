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
    #region AppData
    private AppStorage AppData { get; init; }

    public Preferences Preferences
    {
        get => AppData.Preferences;
        set
        {
            AppData.Preferences = value;
            AppData.Save();
            OnPropertyChanged(nameof(Preferences));
        }
    }
    
    private LibraryConfig? _config = new LibraryConfig("");
    public LibraryConfig? Config
    {
        get => _config;
        set
        {
            SetProperty(ref _config, value);
            if (value != null)
            {
                LibrarySortHeader.Value = (value.LibrarySortOption, value.LibrarySortAscending);
                BookSortHeader.Value = (value.BookSortOption, value.BookSortAscending);
                NodeSortHeader.Value = (value.NodeSortOption, value.NodeSortAscending);
                ShowNavPane = value.ShowNavPane;    
            }
        }
    }

    
    public ObservableCollection<LibraryListItem> Libraries
    {
        get {
                var libraries  = new ObservableCollection<LibraryListItem>();
                foreach (var s in AppData.LibrariesList)
                {
                    libraries.Add(new LibraryListItem()
                    {
                        Name = s,
                        MainViewModel = this
                    });
                }
                return libraries; 
        }
    }

    private LibraryViewModel? _library;
    
    private bool _showNavPane;
    public bool ShowNavPane
    {
        get => _showNavPane;
        set
        {
            SetProperty(ref _showNavPane, value);
            if(value)
                OnPropertyChanged(nameof(Libraries));
            if (Config != null)
            {
                Config.ShowNavPane = value;
            }
        }
    }

    public bool ShowLibraryTree => LibraryRoot != null;
    
    [ObservableProperty]private FullSortHeaderViewModel _librarySortHeader;
    [ObservableProperty]private FullSortHeaderViewModel _bookSortHeader;
    [ObservableProperty]private ShortSortHeaderViewModel _nodeSortHeader;

    #endregion
    
    #region Library exposure
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
            if(LibraryRoot != null)
                LibraryRoot.SelectedNode = value;
        }
    }
    #endregion


    
    
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



    private readonly IDialogService _dialogService;
    public IDialogService DialogService => _dialogService;
    
    

    
    //toolbar
    
    
    [ObservableProperty] private string _searchString = string.Empty;
    
    //status bar
    [ObservableProperty] private string? _imageStatus;
    [ObservableProperty] private string? _pageNameStatus;
    [ObservableProperty] private string? _pageCreatedStatus;
    [ObservableProperty] private string? _bookStatus;
    [ObservableProperty] private string? _libraryStatus;
    [ObservableProperty] private string? _pagingStatus;
    
    private readonly BackgroundFileWatcher _watcher = new();
    ProgressDialogViewModel _progressDialog;
    public MainViewModel(IDialogService dialogService)
    {
        _dialogService = dialogService;
        _cache = new SlidingImageCache(this);
        _librarySortHeader = new FullSortHeaderViewModel();
        _librarySortHeader.StateChanged += OnLibrarySortHeaderStateChanged;
        _bookSortHeader = new FullSortHeaderViewModel();
        _bookSortHeader.StateChanged += OnBookSortHeaderStateChanged;
        _nodeSortHeader = new ShortSortHeaderViewModel();
        _nodeSortHeader.StateChanged += OnNodeSortHeaderStateChanged;
        AppData = AppStorage.Instance;
        _progressDialog = new ProgressDialogViewModel(_dialogService);
    }

    public void Initialize(string? libraryPath)
    {
        if (libraryPath != null)
            OpenLibrary(libraryPath);
    }

    public void OnClose()
    {
        if(!string.IsNullOrEmpty(Library?.Path))
            CloseLibrary();
        else AppData.Save();
        
        _watcher.Dispose();
    }
    
    void OpenLibrary(string? libraryPath)
    {
        _watcher.StopWatching();
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
            var info = new DirectoryInfo(libraryPath);
            Library = config.Recursive ? 
                new LibraryViewModel(this, libraryPath) : 
                new LibraryNodeViewModel(this, libraryPath, info.Name, info.CreationTime, info.LastWriteTime);

            Config = config;            
            AppData.AddOrUpdate(libraryPath, config.Serialize());
            AppData.Save();
            OnPropertyChanged(nameof(Libraries));
//            OnPropertyChanged(nameof(Config));
            
            

            
            IProgress<(string, double, bool)> progress = _progressDialog.Progress;
            Dispatcher.UIThread.Post(()=> _ = _progressDialog.Show(), DispatcherPriority.Render);

            _ = Task.Run(async () =>
            {
                try
                {
                    await _progressDialog.DialogShown;
                    await Task.Delay(64);
                    await Library.StartParsingLibraryAsync(libraryPath, progress);
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                finally
                {
                    //ensure the dialog gets closed after library 
                    progress.Report(("", -1.0, true));
                    //_watcher.BeginBuffering();
                    _watcher.StartWatching(libraryPath, true, Library.FileSystemChanged, Library.FileSystemRenamed);
                    //_watcher.FlushBufferedEvents();
                }

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
                        OnPropertyChanged(nameof(Libraries)); 
                    }    
                    OpenLibrary(selectedPath);
                });
            }
        }
            
    }


    [RelayCommand]
    private async Task EditPreferences()
    {
        if (Library!= null)
        {
            var pref  = Preferences;
            
            var dialogVm = new PreferencesEditorViewModel()
            {
                MouseSensitivity = pref.MouseSensitivity,
                ControllerStickSensitivity = pref.ControllerStickSensitivity
            };
            
            var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            if (window != null)
            {
                var result = await _dialogService.ShowDialogAsync<Preferences>(window, dialogVm);
                if (result != null)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Preferences = result;
                    });
                }    
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
                OnPropertyChanged(nameof(Libraries));
            }    
        }
            
    }
    [RelayCommand]
    void ToggleFullscreen()
    {
        IsFullscreen = !IsFullscreen;
        var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow : null;
        (window as MainWindow)?.ToggleFullscreen();
        OnPropertyChanged(nameof(Config));
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
    void OnLibrarySortHeaderStateChanged( object? _, EventArgs __ )
    {
        if (Config != null)
        {
            Config.LibrarySortOption = LibrarySortHeader.Value.sortOption;
            Config.LibrarySortAscending = LibrarySortHeader.Value.ascending;
        }
        if (Library != null)
        {
            if (Library.CurrentSortOption != LibrarySortHeader.Value.sortOption)
                Library.Sort(LibrarySortHeader.Value.sortOption, LibrarySortHeader.Value.ascending);
            else if (Library.CurrentSortAscending != LibrarySortHeader.Value.ascending)
                Library.ReverseSortOrder();    
        }
    }
    void OnBookSortHeaderStateChanged(object? _, EventArgs __)
    {
        if (Config != null)
        {
            Config.BookSortOption = BookSortHeader.Value.sortOption;
            Config.BookSortAscending = BookSortHeader.Value.ascending;
        }
        if (SelectedBook != null)
        {
            if (SelectedBook.CurrentSortOption != BookSortHeader.Value.sortOption)
                SelectedBook.Sort(BookSortHeader.Value.sortOption, BookSortHeader.Value.ascending);
            else if (SelectedBook.CurrentSortAscending != BookSortHeader.Value.ascending)
                SelectedBook.ReverseSortOrder();    
        }
    }
    public void OnNodeSortHeaderStateChanged(object? _, EventArgs __)
    {
        var option = NodeSortHeader.Value.sortOption;
        var asc = NodeSortHeader.Value.ascending;
        if (Config != null)
        {
            Config.NodeSortOption = option;
            Config.NodeSortAscending = asc;    
        }
        if (Library is LibraryNodeViewModel node)
        {
            if(option != node.Root.CurrentChildrenSortOption)
                node.Root.SortChildren(option, asc);
            else if (asc != node.Root.CurrentChildrenSortAscending)
                node.Root.ReverseChildrenSortOrder();
        }
    }
    
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