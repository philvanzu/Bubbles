using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
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

    [ObservableProperty] private LibraryConfig? _config = new LibraryConfig("");

    partial void OnConfigChanged(LibraryConfig? value)
    {
        if (value != null)
        {
            LibrarySortHeader.Value = (value.LibrarySortOption, value.LibrarySortAscending);
            BookSortHeader.Value = (value.BookSortOption, value.BookSortAscending);
            NodeSortHeader.Value = (value.NodeSortOption, value.NodeSortAscending);
        }

        OnPropertyChanged(nameof(ShowNavPane));
    }

    public bool ShowNavPane => Config?.ShowNavPane ?? false;
    public ObservableCollection<LibraryListItem> Libraries => MakeLibraries();

    ObservableCollection<LibraryListItem> MakeLibraries()
    {
        var libraries = new ObservableCollection<LibraryListItem>()
            { new LibraryListItem() { Name = "Add New Library" } };
        foreach (var s in AppData.LibrariesList)
            libraries.Add(new LibraryListItem() { Name = s });
        return libraries;
    }

    public string LibraryName => Library == null ? "Select Library" : Library.Path;

    [ObservableProperty] private LibraryListItem? _selectedLibraryItem;

    partial void OnSelectedLibraryItemChanged(LibraryListItem? value)
    {
        if (value == null) return;
        if (value.Name == "Add New Library") CreateLibrary();
        else OpenLibrary(value.Name);
    }

    [ObservableProperty] private FullSortHeaderViewModel _librarySortHeader;
    [ObservableProperty] private FullSortHeaderViewModel _bookSortHeader;
    [ObservableProperty] private ShortSortHeaderViewModel _nodeSortHeader;

    #endregion
//test modif for commit
    #region Library exposure

    [ObservableProperty] private LibraryViewModel? _library;

    partial void OnLibraryChanged(LibraryViewModel? value)
    {
        if (value is LibraryNodeViewModel node)
        {
            if (node.Parent == null && LibraryRoot != node) LibraryRoot = node;
            if (node.Parent != null && SelectedLibraryNode != node) SelectedLibraryNode = node;
        }
        else LibraryRoot = null;
    }

    [ObservableProperty] private LibraryNodeViewModel? _libraryRoot;

    [ObservableProperty] private LibraryNodeViewModel? _selectedLibraryNode;

    partial void OnSelectedLibraryNodeChanged(LibraryNodeViewModel? value)
    {
        if (value != null && value != Library)
        {
            Library = value;
            Library.Sort();
        }

        if (LibraryRoot != null)
        {
            if (LibraryRoot.SelectedNode != null)
            {
                if (LibraryRoot.SelectedNode.SelectedItem != null)
                    LibraryRoot.SelectedNode.SelectedItem.IsSelected = false;
            }
            LibraryRoot.SelectedNode = value;
        }
            
    }
    

    #endregion

    [ObservableProperty] public BookViewModel? _selectedBook;
    [ObservableProperty] private ViewerData? _currentViewerData;
    [ObservableProperty] private PageViewModel? _currentPageViewModel;
    [ObservableProperty] private bool _isFullscreen;

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
    public SlidingImageCache _cache;

    private readonly IDialogService _dialogService;
    public IDialogService DialogService => _dialogService;
    ProgressDialogViewModel _progressDialog;
    [ObservableProperty] private ProgressViewModel _statusProgress = new();
    
    private MainWindow _window;
    public MainWindow? MainWindow { 
        get => _window;
        set
        {
            if (value == null) return;
            _window = value;
            ShutdownCoordinator.Window = value;
        }
    }

    public ShutdownCoordinator ShutdownCoordinator { get; private set; } = new();
    

    public MainViewModel(MainWindow window, IDialogService dialogService)
    {
        _window = window;
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


    public void OnShutdown()
    {
        if (ShutdownCoordinator.IsShuttingDown == true) return;
        ShutdownCoordinator.IsShuttingDown = true;

        if (Library != null) 
            CloseLibrary();
        
        _watcher.StopWatching();
    }
    
    [RelayCommand]
    void OpenLibrary(string? libraryPath)
    {
        
        if(Library != null)
            CloseLibrary();
        
        if (!string.IsNullOrEmpty(libraryPath) && Directory.Exists(libraryPath))
        {
            libraryPath = libraryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            libraryPath += Path.DirectorySeparatorChar;
            
            var config = AppData.GetConfig(libraryPath) ?? new LibraryConfig(libraryPath);

            var info = new DirectoryInfo(libraryPath);
            Library = config.Recursive ? 
                new LibraryViewModel(this, libraryPath) : 
                new LibraryNodeViewModel(this, libraryPath, info.Name, info.CreationTime, info.LastWriteTime);

            Config = config;            
            AppData.AddOrUpdate(libraryPath, config.Serialize());
            AppData.Save();
            OnPropertyChanged(nameof(Libraries));
            OnPropertyChanged(nameof(LibraryName));
            OnPropertyChanged(nameof(Config));
            
            IProgress<(string, double, bool)> progress = _progressDialog.Progress;
            Dispatcher.UIThread.Post(()=> _ = _progressDialog.Show(), DispatcherPriority.Render);

            _ = Task.Run(async () =>
            {
                if (Config.CacheLibraryData && Config.Recursive && Directory.Exists(libraryPath))
                {
                    try
                    {
                        string libraryDataCachePath = Path.Combine(Library.Path, ".bblLibraryData");
                        var json = File.ReadAllText(libraryDataCachePath);

                        await _progressDialog.DialogShown;
                        // fast cache load will report progress to the progress dialog
                        await Library.LoadSerializedCollection(json, progress);
                        await Task.Delay(1);
                        await Dispatcher.UIThread.InvokeAsync(() => {}, DispatcherPriority.Background);
                        // slow parsing will report progress to the status bar
                        progress = StatusProgress.Progress;

                    }
                    catch
                    {
                        //fast load failed, parsing process will report to the progress dialog
                        progress = _progressDialog.Progress;
                        await _progressDialog.DialogShown;
                    }
                }

                try
                {
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
                    _watcher.StartWatching(libraryPath, true, Library.FileSystemChanged);
                    //_watcher.FlushBufferedEvents();
                }

            });
        }
    }
    
    [RelayCommand]
    private void CloseLibrary()
    {
        _watcher.StopWatching();
        if (Library != null)
        {
            if ( Library is LibraryNodeViewModel == false && Config?.CacheLibraryData==true)
            {
                string json = Library.SerializeCollection();
                string path =  Path.Combine(Library.Path, ".bblLibraryData");
                if (Directory.Exists(Library.Path))
                {
                    File.WriteAllText(path, json);
                }
            }
            CurrentViewerData = null;
            OnPropertyChanged(nameof(CurrentViewerData));

            Library.Close();
            if (Config != null)
            {
                var save = Library;
                if (Library is LibraryNodeViewModel library) save = library.Root;
                AppData.AddOrUpdate(save.Path, Config.Serialize());
                AppData.Save();
                Config = null;
            }
            _cache.ClearCache();
            Library = null;
            if(LibraryRoot!=null) LibraryRoot = null;
            if(SelectedBook!= null) SelectedBook = null;
            if(SelectedLibraryNode != null) SelectedLibraryNode = null;
            if(CurrentPageViewModel != null) CurrentPageViewModel = null;
            OnPropertyChanged(nameof(LibraryName));
            OnPropertyChanged(nameof(Config));
            
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
    private void CreateLibrary()
    {
        var dlgVm = new LibraryConfigViewModel(new LibraryConfig("Pick a Directory"), _dialogService);
        CreateOrUpdateLibrary(dlgVm);
    }
    [RelayCommand]
    private void ConfigureLibrary()
    {
        if (Library == null) return;
        var config = Config;
        if (config == null) config = AppData.GetConfig(Library.Path);
        if (config == null) config = new LibraryConfig(Library.Path);
        
        var dlgVm = new LibraryConfigViewModel(config);
        CreateOrUpdateLibrary(dlgVm);
    }
    
    private void CreateOrUpdateLibrary(LibraryConfigViewModel dialogVm)
    {

        if (MainWindow != null)
        {
            Dispatcher.UIThread.InvokeAsync(async() =>
            {
                try
                {
                    var result = await _dialogService.ShowDialogAsync<LibraryConfig>(MainWindow, dialogVm);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (result != null)
                        {
                            AppData.AddOrUpdate(result.Path, result.Serialize());
                            AppData.Save();
                            Config = result;
                            
                            if (dialogVm.IsCreatingLibrary)
                            {
                                OnPropertyChanged(nameof(Libraries));
                                OpenLibrary(result.Path);    
                            }
                            
                        }

                    });
                }
                catch(Exception ex){Console.WriteLine(ex);}
            });
        }
    }
    

    [RelayCommand]
    private async Task EditPreferences(string? showInputTab=null)
    {
        var dialogVm = new UserSettingsEditorViewModel(_dialogService);
        
        if (MainWindow != null)
        {
            var dlgWin = new UserSettingsEditorView(dialogVm);
            if (showInputTab == "true")
            {
                dlgWin.Tab2Toggle.IsChecked = true;
                dlgWin.TabButtonToggled(dlgWin.Tab2Toggle, new());
            }
            var result = await dlgWin.ShowDialog<UserSettings?>(MainWindow);
            if (result != null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AppData.UserSettings = result;
                    AppData.Save();
                });
            }
        }
    }

    [RelayCommand]
    private void DeleteLibrary()
    {
        if(Library==null) return;
        var path = Library.Path;
        CloseLibrary();
        Dispatcher.UIThread.Post(async void () => await DeleteLibraryPressedAsync(path));
    }
    [RelayCommand]
    private async Task DeleteLibraryPressedAsync(string path)
    {
        if (path == Library?.Path)
            CloseLibrary();

        var dialog = new OkCancelViewModel
        {
            Content = $"Do you want to delete the recorded setting for the library at [{path}] ?"
        };

        if (MainWindow != null)
        {
            var result = await _dialogService.ShowDialogAsync<bool>(MainWindow, dialog);
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

        MainWindow?.ToggleFullscreen();
        OnPropertyChanged(nameof(Config));
    }
    [RelayCommand] private void ExitFullScreen()
    {
        IsFullscreen = false;
        
        MainWindow?.ExitFullscreen();
        OnPropertyChanged(nameof(Config));
    }

    [RelayCommand]
    private void EnterFullScreen()
    {
        IsFullscreen = true;
        MainWindow?.EnterFullscreen();
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
        /*
        if (Config != null)
        {
            Config.LibrarySortOption = LibrarySortHeader.Value.sortOption;
            Config.LibrarySortAscending = LibrarySortHeader.Value.ascending;
        }
        */
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
        /*
        if (Config != null)
        {
            Config.BookSortOption = BookSortHeader.Value.sortOption;
            Config.BookSortAscending = BookSortHeader.Value.ascending;
        }
        */
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
        /*
        if (Config != null)
        {
            Config.NodeSortOption = option;
            Config.NodeSortAscending = asc;    
        }
        */
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
            if (SelectedBook != null)
            {
                Library.RequestScrollToIndex(Library.GetSelectedIndex());
                book = SelectedBook;
            }
            bool more;
            do
            {
                //load book pages, 
                if (! book!.IsSelected)
                    book.IsSelected = true;
                
                if (book.LoadingTask != null)
                {
                    //wait for the pages list loading task to complete.
                    await book.LoadingTask;
                    //Console.WriteLine($"loading task status : {book.LoadingTask.Status}");
                }

                if (book.PageCount == 0) return;

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
            if (SelectedBook != null)
            {
                Library.RequestScrollToIndex(Library.GetSelectedIndex());
                book = SelectedBook;
            }
            bool more;
            do
            {
                //load book pages, 
                if (! book!.IsSelected)
                    book.IsSelected = true;
                
                if (book.LoadingTask != null)
                    //wait for the pages list loading task to complete.
                    await book.LoadingTask;
                if(book.PageCount == 0) return;
                //select previous page
                more = !book.PreviousPage();
                
                if(more) 
                {
                    // if next page doesn't exist in the selected book
                    Library.PreviousBook();
                    book = SelectedBook;
                    //Console.WriteLine($"prevbook {book.Name} :: {book.PageCount} ");
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