using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Linq;
using Bubbles4.Models;
using System.Threading.Tasks;
using Avalonia.Threading;
using Bubbles4.Controls;
using Bubbles4.Services;
using Bubbles4.Views;

namespace Bubbles4.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    #region AppData


    public ObservableCollection<LibraryListItem> Libraries => MakeLibraries();

    ObservableCollection<LibraryListItem> MakeLibraries()
    {
        var libraries = new ObservableCollection<LibraryListItem>()
            { new LibraryListItem() { Name = "Add New Library" } };
        foreach (var s in AppStorage.Instance.LibrariesList)
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

    [ObservableProperty] private bool _showNavPane = false;
    [ObservableProperty] private bool _showBookmarksMenu = false;
    #endregion

    #region Library exposure

    [ObservableProperty] private LibraryViewModel? _library;
    

    #endregion

    [ObservableProperty] public BookViewModel? _selectedBook;

    partial void OnSelectedBookChanged(BookViewModel? value)
    {
        OnPropertyChanged(nameof(CanCheckPreviewIvp));
      //  PageCount = value?.PageCount ?? 1;
        GotoPageNumber = 1;
    }
    //[ObservableProperty] private int _pageCount;
    [ObservableProperty] private ViewerData? _currentViewerData;
    [ObservableProperty] private PageViewModel? _currentPageViewModel;
    [ObservableProperty] private bool _isFullscreen;

    //toolbar
    [ObservableProperty] private string _searchString = string.Empty;

    [ObservableProperty] private bool _previewIVPIsChecked;
    public bool CanCheckPreviewIvp => SelectedBook != null && Library?.Config.UseIVPs == true;
    
    partial void OnPreviewIVPIsCheckedChanged(bool value)
    {
        if(SelectedBook != null)
            SelectedBook.PreviewIvp(value);
    }

    //status bar
    [ObservableProperty] private string? _pageStatus;
    [ObservableProperty] private string? _bookStatus;
    [ObservableProperty] private string? _libraryStatus;
    [ObservableProperty] private string? _pagingStatus;

    private readonly BackgroundFileWatcher _watcher = new();
    public SlidingImageCache _cache;

    private readonly IDialogService _dialogService;
    public IDialogService DialogService => _dialogService;
    ProgressDialogViewModel _progressDialog;
    
    [ObservableProperty] private ProgressViewModel _statusProgress;
    
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
    public FastImageViewer? ViewerControl { get; set; }
    


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
        

        _progressDialog = new ProgressDialogViewModel(_dialogService);
        _statusProgress = new ProgressViewModel();
    }

    public void Initialize(string? libraryPath)
    {
        if (libraryPath != null)
            OpenLibrary(libraryPath);
    }

    [RelayCommand] private void ShutdownRequested()
    {
        MainWindow?.Close();
    }
    public void OnShutdown()
    {
        if (ShutdownCoordinator.IsShuttingDown) return;
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

            bool newconfig = false;
            var config = AppStorage.Instance.GetConfig(libraryPath);
            if (config == null)
            {
                config = new LibraryConfig(libraryPath);
                newconfig = true;
            }
            BookViewModel.CurrentSortOption = config.BookSortOption;
            BookViewModel.CurrentSortAscending = config.BookSortAscending;

            var info = new DirectoryInfo(libraryPath);
            Library = new LibraryViewModel(this, libraryPath, config);
            
            if (newconfig)
            {
                AppStorage.Instance.AddOrUpdate(libraryPath, config.Serialize());
                AppStorage.Instance.Save();
            }
            OnPropertyChanged(nameof(Libraries));
            OnPropertyChanged(nameof(LibraryName));
            
            
            IProgress<(string, double, bool)> progress = _progressDialog.Progress;
            Dispatcher.UIThread.Post(()=> _ = _progressDialog.Show(), DispatcherPriority.Render);

            _ = Task.Run(async () =>
            {
                if (Library.Config.CacheLibraryData  && Directory.Exists(libraryPath))
                {
                    try
                    {
                        string libraryDataCachePath = Path.Combine(Library.Path, ".bblLibraryData");
                        var json = File.ReadAllText(libraryDataCachePath);

                        await _progressDialog.DialogShown;
                        // fast cache load will report progress to the progress dialog
                        var success = await Library.LoadSerializedCollection(json, progress);
                        await Task.Delay(1);
                        await Dispatcher.UIThread.InvokeAsync(() => {}, DispatcherPriority.Background);
                        // slow parsing will report progress to the status bar
                        if(success) progress = StatusProgress.Progress;
                        else throw new Exception("Failed to load library data");
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
                    progress.Report(("", 0, true));
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
            
            if ( Library.Config.CacheLibraryData==true)
            {
                string json = Library.Serialize();
                string path =  Path.Combine(Library.Path, ".bblLibraryData");
                if (Directory.Exists(Library.Path))
                {
                    File.WriteAllText(path, json);
                }
            }
            CurrentViewerData = null;
            OnPropertyChanged(nameof(CurrentViewerData));
            
            Library.Close();
            
            _cache.ClearCache();
            Library = null;
            
            if(SelectedBook!= null) SelectedBook = null;
            if(CurrentPageViewModel != null) CurrentPageViewModel = null;
            OnPropertyChanged(nameof(LibraryName));
            ShowBookmarksMenu = false;
            ShowNavPane = false;
        }  
    }

    partial void OnCurrentViewerDataChanged(ViewerData? value)
    {
        _ = value;
        UpdatePageStatus();
        UpdatePagingStatus();
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
        
        var dlgVm = new LibraryConfigViewModel(Library.Config);
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
                            AppStorage.Instance.AddOrUpdate(result.Path, result.Serialize());
                            AppStorage.Instance.Save();
                            
                            if (dialogVm.IsCreatingLibrary)
                            {
                                OnPropertyChanged(nameof(Libraries));
                                OpenLibrary(result.Path);
                            }

                            if (Library != null)
                            {
                                Library.Config = new("dummy");
                                Library.Config = result;
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
            var dlgWin = new UserSettingsEditorDialog(dialogVm);
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
                    AppStorage.Instance.UserSettings = result;
                    InputManager.Instance.SaveBindings();
                    AppStorage.Instance.Save();
                });
            }
        }
    }




    [RelayCommand]
    private void RefreshLibrary()
    {
        OpenLibrary(Library!.Path);
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
                AppStorage.Instance.Remove(path);
                AppStorage.Instance.Save();
                OnPropertyChanged(nameof(Libraries));
            }    
        }
            
    }
    [RelayCommand]
    void ToggleFullscreen()
    {
        IsFullscreen = !IsFullscreen;

        MainWindow?.ToggleFullscreen();
    }
    [RelayCommand] private void ExitFullScreen()
    {
        if (!IsFullscreen) return;
        IsFullscreen = false;
        MainWindow?.ExitFullscreen();
    }

    [RelayCommand]
    private void EnterFullScreen()
    {
        if (IsFullscreen) return;
        IsFullscreen = true;
        MainWindow?.EnterFullscreen();
    }

    [RelayCommand]
    private void Search(string? keywords)
    {
        // You can parse keywords here or pass them directly to your filtering method
        var keywordList = keywords?
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        Library?.Filter(keywordList);
    }
    [RelayCommand]
    private void ClearSearch()
    {
        SearchString = string.Empty;
        Search(null);
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
            if (BookViewModel.CurrentSortOption != BookSortHeader.Value.sortOption)
                SelectedBook.Sort(BookSortHeader.Value.sortOption, BookSortHeader.Value.ascending);
            else if (BookViewModel.CurrentSortAscending != BookSortHeader.Value.ascending)
                SelectedBook.ReverseSortOrder();    
        }
    }
    public void OnNodeSortHeaderStateChanged(object? _, EventArgs __)
    {
        var option = NodeSortHeader.Value.sortOption;
        var asc = NodeSortHeader.Value.ascending;
        
        if (Library != null)
        {
            if(option != Library.RootNode.CurrentSort)
                Library.RootNode.SortChildren(option, asc);
            else if (asc != Library.RootNode.CurrentAscending)
                Library.RootNode.ReverseChildrenSortOrder();
        }
    }
    [ObservableProperty] private int _gotoPageNumber=1;
    [RelayCommand]
    private void GotoPage()
    {
        if(SelectedBook == null) return;
        
        int idx = GotoPageNumber - 1;
        if(idx < 0 ) idx = 0;
        else if (idx >= SelectedBook.PageCount) idx = SelectedBook.PageCount - 1;
        SelectedBook.Pages[idx].IsSelected = true;
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
            LibraryStatus = String.Format($"Library Path : {Library.Path} | BookCount : {Library.Count}");    
        }
        else LibraryStatus = $"No Library Loaded"; 
    }
    public void UpdateBookStatus()
    {
        if (Library?.SelectedItem != null)
            BookStatus = Library.SelectedItem.Path;
        else BookStatus = "";
    }
    public void UpdatePageStatus()
    {
        if (Library?.SelectedItem?.SelectedPage != null)
        {
            PageStatus = $"{Library.SelectedItem.SelectedPage.Name} | " +
                         $"Created : {Library.SelectedItem.SelectedPage.Created} | " +
                         $"Modified : {Library.SelectedItem.SelectedPage.Modified} | " +
                         $"{CurrentViewerData?.Image?.PixelSize.Width}px X {CurrentViewerData?.Image?.PixelSize.Height}px";
        }
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
        PagingStatus = $"Page {idx} / {total}";
    }
    #endregion

}