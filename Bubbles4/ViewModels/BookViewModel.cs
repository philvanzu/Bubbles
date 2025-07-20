using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Bubbles4.Models;
using Bubbles4.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;

namespace Bubbles4.ViewModels;

public partial class BookViewModel: ViewModelBase, ISelectableItem, ISelectItems
{
    private LibraryViewModel _library;
    private BookBase _model;
    public BookBase Model => _model;
    
    public string Path => _model.Path;
    public string Name => _model.Name;
    public int PageCount => _pages.Count;
    public DateTime LastModified => _model.LastModified;
    public DateTime Created => _model.Created;
    public int RandomIndex;
    public LibraryConfig.SortOptions CurrentSortOption { get; set; }
    public bool CurrentSortAscending { get; set; } 
    public Task? LoadingTask { get; set; }

    List<PageViewModel> _pages = new ();
    ObservableCollection<PageViewModel> _pagesMutable = new ();
    public ReadOnlyObservableCollection<PageViewModel> Pages { get; }
    
    [ObservableProperty] private BookMetadata? _ivps;
    [ObservableProperty] bool _isSelected;
    [ObservableProperty]Bitmap? _thumbnail;
    [ObservableProperty]MainViewModel _mainViewModel;
    
    private bool _isThumbnailLoading;

    public event EventHandler<SelectedItemChangedEventArgs>? SelectionChanged;
    public event EventHandler? SortOrderChanged;
    public event EventHandler<int>? ScrollToIndexRequested;
    Object? _bookmarkBlocker;
    public BookViewModel(BookBase book, LibraryViewModel library, MainViewModel mainViewModel)
    {
        this._mainViewModel = mainViewModel;
        this._model = book;
        this._library = library;
        
        Pages = new ReadOnlyObservableCollection<PageViewModel>(_pagesMutable);
        CurrentSortOption = _mainViewModel.Config?.BookSortOption ?? LibraryConfig.SortOptions.Path;
        CurrentSortAscending = _mainViewModel.Config?.BookSortAscending ?? true;
    }

    ~BookViewModel()
    {
        UnloadPagesList();
        if(Thumbnail != null)
            Thumbnail.Dispose();
    }
    
    partial void OnIsSelectedChanged(bool oldValue, bool newValue)
    {
        
        if (newValue && _library.SelectedItem != this) _library.SelectedItem = this;
        else if (oldValue && _library.SelectedItem == this) _library.SelectedItem = null;

        if (!newValue && oldValue) UnloadPagesList();
    }
    public async Task LoadPagesListAsync()
    {
        var pgs = await _model.LoadPagesList();
        if (pgs == null) Console.WriteLine($"null pages list returned for {Path}");
        else
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _pages.Clear();

                if (pgs.Count > 0)
                {
                    foreach (var page in pgs)
                    {
                        _pages.Add(new PageViewModel(this, page));
                        _model.PagesCts.TryAdd(page.Path, null);
                    }
                }
                Sort();                
                Model.PageCount = _pages.Count;

                Ivps = BookMetadata.Load(_model.IvpPath);
                if (Ivps.Collection.Count > 0)
                {
                    var pgNames = pgs.Select(p => p.Name).ToList();
                    var ivpNames = Ivps.Collection.Select(x => x.filename).ToArray();
                    foreach (var name in ivpNames)
                        if(!pgNames.Contains(name))
                            Ivps.Remove(name); 
                }
                
                //bookmark loading
                if (MainViewModel?.Config?.LookAndFeel == LibraryConfig.LookAndFeels.Reader)
                {
                    if (File.Exists(Model.BookmarkPath))
                    {
                        Dispatcher.UIThread.Post(()=> _ = PromptLoadBookmark()); 
                    }
                }
            });
        }
    }

    public void UnloadPagesList()
    {
        
        var selectedIdx = GetPageIndex(SelectedPage);
        bool bookmark = selectedIdx > 0 
                        && selectedIdx < PageCount - 1 
                        && MainViewModel?.Config?.LookAndFeel == LibraryConfig.LookAndFeels.Reader;
        if (bookmark)
        {
            var name = SelectedPage!.Name;
            if (MainViewModel?.ShutdownCoordinator.IsShuttingDown == true)
            {
                _bookmarkBlocker = new Object();
                MainViewModel.ShutdownCoordinator.RegisterBlocker(_bookmarkBlocker);
            }
                
            Dispatcher.UIThread.Post(()=> _ = PromptBookmarkSelectedPage(name));
        }
        
        if (Ivps != null)
        {
            MainViewModel?.ViewerControl?.OnBookClosing();
            Ivps.Save(_model.IvpPath);
            Ivps = null;
        }
        
        _model.CancelPagesListLoad();
        foreach (var page in Pages) page.Unload();
        if (Pages.Count > 0)
        {
            _pages.Clear();
            _pagesMutable.Clear();
        }

        
        if (_model.PagesCts.Count > 0)
        {
            foreach (var kv in _model.PagesCts)
            {
                kv.Value?.Cancel();
                kv.Value?.Dispose();
            }
            _model.PagesCts.Clear();
        }    
        
        SelectedPage = null;
    }


    
    public void PrepareThumbnail()
    {
        bool firstbook = _library.GetBookIndex(this) == 0;
        if (firstbook)
        {
            //Console.WriteLine("firstbook prepared");
        }

        if (_isThumbnailLoading) return;
        
        _isThumbnailLoading = true;
        _ = Task.Run(async () =>
        {
            try
            {
                var bitmap = await Model.LoadThumbnailAsync();
                if(bitmap != null)
                    Dispatcher.UIThread.Post(()=>{
                        if (Thumbnail != null)
                            Thumbnail.Dispose();

                        Thumbnail = bitmap;
                    }); 
            }
            catch (Exception ex){Console.WriteLine(ex);}
            finally
            {
                _isThumbnailLoading = false;
            }
        });
        
    }
    
    //__book0__issue__hack
    public bool IsFirstBook=>_library.GetBookIndex(this) == 0;
    
    public void ClearThumbnail()
    {
        if (_isThumbnailLoading) _model.CancelThumbnailLoad();
        Thumbnail?.Dispose();
        Thumbnail = null;
    
        //Console.WriteLine($"unloading thmbnail idx {} at :{Name}");
    }
    
    [RelayCommand]
    private void PagePrepared(object? parameter)
    {
        if (parameter is PageViewModel vm)
        {
            PreparePageThumbnail(vm);
        }
            
    }

    [RelayCommand]
    private void PageClearing(object? parameter)
    {
        if (parameter is PageViewModel vm)
        {
            vm.Unload();       
        }
            
    }
    public void PreparePageThumbnail(PageViewModel page)
    {
        if ( page.IsThumbnailLoading )
            return;
        
        page.IsThumbnailLoading = true;
        _ = Task.Run(async () =>
        {
            try
            {
                var bitmap = await _model.LoadThumbnailAsync(page.Path);
                if (bitmap != null)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (page.Thumbnail != null)
                            page.Thumbnail.Dispose();

                        page.Thumbnail = bitmap.Value.Item1;
                        page.ImageSize = bitmap.Value.Item2;
                        if(MainViewModel.PreviewIVPIsChecked)
                            page.ShowIvpRect();
                        else page.IvpRectVisible = false;
                    });
                }
            }
            finally
            {
                page.IsThumbnailLoading = false;
            }
        });

    }
    


    [RelayCommand]
    public async Task OnSelection()
    {
        IsSelected = true;
        if(SelectedPage != null) SelectedPage.IsSelected = false;
        await MainViewModel.Next();
    }
    private IComparer<PageViewModel> GetComparer(LibraryConfig.SortOptions sort, bool ascending)
    {
        return sort switch
        {
            LibraryConfig.SortOptions.Path => ascending
                ? SortExpressionComparer<PageViewModel>.Ascending(x => x.Path)
                : SortExpressionComparer<PageViewModel>.Descending(x => x.Path),

            LibraryConfig.SortOptions.Alpha => ascending
                ? SortExpressionComparer<PageViewModel>.Ascending(x => x.Name)
                : SortExpressionComparer<PageViewModel>.Descending(x => x.Name),

            LibraryConfig.SortOptions.Created => ascending
                ? SortExpressionComparer<PageViewModel>.Ascending(x => x.Created)
                : SortExpressionComparer<PageViewModel>.Descending(x => x.Created),

            LibraryConfig.SortOptions.Modified => ascending
                ? SortExpressionComparer<PageViewModel>.Ascending(x => x.LastModified)
                : SortExpressionComparer<PageViewModel>.Descending(x => x.LastModified),

            LibraryConfig.SortOptions.Natural => new PageViewModelNaturalComparer(ascending),

            LibraryConfig.SortOptions.Random => 
                SortExpressionComparer<PageViewModel>.Ascending(x => x.RandomIndex),

            _ => SortExpressionComparer<PageViewModel>.Ascending(x => x.Name)
        };
    }

    public void ShufflePages()
    {
        foreach (var page in _pages)
            page.RandomIndex = CryptoRandom.NextInt();
    }
    public void Sort(LibraryConfig.SortOptions? sort=null, bool? ascending=null)
    {
        if(sort == null) sort  = MainViewModel.Config?.BookSortOption ?? LibraryConfig.SortOptions.Path;
        if(ascending == null) ascending = MainViewModel.Config?.BookSortAscending ?? true;
        CurrentSortOption = sort.Value;
        CurrentSortAscending = ascending.Value;
        if(sort == LibraryConfig.SortOptions.Random)ShufflePages();
        var comparer = GetComparer(CurrentSortOption, CurrentSortAscending);
        var sorted = _pages.OrderBy(p => p, comparer);

        _pagesMutable.Clear();
        _pagesMutable.AddRange(sorted);

        OnPropertyChanged(nameof(Pages));
        //InvokeSortOrderChanged();
    }

    public void InvokeSortOrderChanged()
    {
        SortOrderChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReverseSortOrder()
    {
        CurrentSortAscending = !CurrentSortAscending;
        Sort(CurrentSortOption, CurrentSortAscending);
    }
    [RelayCommand]
    private async Task Delete()
    {
        var dialog = new OkCancelViewModel
        {
            Title = "Delete File?",
            Content = $"Do you want to delete [{Path}] permanently?"
        };
        var window = MainViewModel.MainWindow;
        if (window != null)
        {
            var result = await MainViewModel.DialogService.ShowDialogAsync<bool>(window, dialog);
            if (result)
            {
                try
                {
                    if (File.Exists(Path)) File.Delete(Path);
                    else if (Directory.Exists(Path)) Directory.Delete(Path, true);
                    else Console.Error.WriteLine($"Path not found: {Path}");

                    // Wait a tick in case the OS needs to release file handles
                    await Task.Yield();
                }
                catch (Exception ex) { Console.Error.WriteLine($"Hard delete failed: {ex.Message}"); }
            }    
        }
    }
    
    [RelayCommand]
    private void OpenInExplorer()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Path}\"") { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Try with xdg-open (common across most Linux desktop environments)
                Process.Start(new ProcessStartInfo("xdg-open", $"\"{Path}\"") { UseShellExecute = true });
            }
            else
            {
                throw new PlatformNotSupportedException("Only Windows and Linux are supported.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to open path: {ex.Message}");
        }
    }

    
    [RelayCommand]
    private void SaveCroppedIvpsToSize()
    {
        var progress = MainViewModel.StatusProgress;
        _ = Task.Run(async () =>
        {
            try
            {
                int maxSize = 5000;
                string prefix = "_";
                string suffix = "_5000.png";
                int batchSize = 8;

                var factories = new Queue<Func<Task>>();
                foreach (var page in _pages.ToList())
                {
                    var cropRect = page.GetIvpCropRect();
                    var directory = Model.MetaDataPath;
                    string name = $"{prefix}{page.Name}{suffix}";
                    string path = System.IO.Path.Combine(directory, name);

                    factories.Enqueue(() => Model.SaveCroppedIvpToSizeAsync(page, path, cropRect, maxSize));
                }

                int count = 0;
                int total = factories.Count;

                progress.OnProgressUpdated(($"cropping task 0 of {total}", 0.0, false));

                while (factories.Count > 0)
                {
                    var batch = new List<Task>();
                    while (batch.Count < batchSize && factories.Count > 0)
                    {
                        var taskFactory = factories.Dequeue();
                        batch.Add(taskFactory());
                        count++;
                    }

                    await Task.WhenAll(batch);
                    progress.OnProgressUpdated(($"cropping task {count} of {total}", (double)count / total, false));
                }

                progress.OnProgressUpdated(("", 1.0, true));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        });
    }

    
    public async Task PromptBookmarkSelectedPage( string pageName)
    {
        var dialog = new OkCancelViewModel
        {
            Title ="Bookmark current page?",
            Content = $"Do you want to bookmark your current position in {Name}?",
        };
        var window = MainViewModel.MainWindow;
        if (window != null)
        {
            var result = await MainViewModel.DialogService.ShowDialogAsync<bool>(window, dialog);
            if (result)
            {
                try
                {
                    File.WriteAllText(Model.BookmarkPath, pageName);
                    if (_bookmarkBlocker != null)
                    {
                        MainViewModel.ShutdownCoordinator.UnregisterBlocker(_bookmarkBlocker);
                        _bookmarkBlocker = null;
                    }
                        
                }
                catch (Exception ex) { Console.Error.WriteLine($"bookmark file creation failed: {ex.Message}"); }
            }    
        }
    }

    public async Task PromptLoadBookmark()
    {
        string? name = null;
        try
        {
            name = File.ReadAllText(Model.BookmarkPath);
        }
        catch (Exception ex) {Console.WriteLine(ex); }
        finally
        {
            try {File.Delete(Model.BookmarkPath);}
            catch (Exception ex) {Console.WriteLine(ex); }
        }

        PageViewModel? bookmarked = null;
        if (name != null)
            bookmarked = _pages.FirstOrDefault(x => x.Name == name);
        if (bookmarked != null)
        {
            var dialog = new OkCancelViewModel
            {
                Title ="Load Bookmark?",
                Content = $"Do you want to load bookmarked page [{bookmarked.Name}]?"
            };
            var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            if (window != null)
            {
                var result = await MainViewModel.DialogService.ShowDialogAsync<bool>(window, dialog);
                if (result)
                {
                    bookmarked.IsSelected = true;
                }    
            }    
        }
    }

    public void RequestScrollToIndex(int index)
    {
        ScrollToIndexRequested?.Invoke(this, index);
    }
    public int GetSelectedIndex()
    {
        return GetPageIndex(SelectedPage);
    }
    public int GetPageIndex(PageViewModel? pageViewModel)
    {
        if(pageViewModel == null)return -1;
        return Pages.IndexOf(pageViewModel);
    }

    
    [ObservableProperty] PageViewModel? _selectedPage;

    //private CancellationTokenSource? _imgLoadCts;
    partial void OnSelectedPageChanged(PageViewModel? value)
    {
        PageViewModel? oldSelected=null;
        foreach (var page in Pages)
        {
            if (page != value && page.IsSelected)
            {
                oldSelected = page;
                //unload page in PageView and dispose pages data
                page.IsSelected = false;
            }
        }
        _mainViewModel.CurrentPageViewModel = value;
        InvokeSelectionChanged(value, oldSelected);
        _mainViewModel.UpdatePageNameStatus();
    }

    public void InvokeSelectionChanged(ISelectableItem? newItem, ISelectableItem? oldItem)
    {
        SelectionChanged?.Invoke(this, new SelectedItemChangedEventArgs(this, newItem, oldItem));
    }



    public bool NextPage()
    {
        int index = -1;
        if(SelectedPage != null) index = GetPageIndex(SelectedPage);
        
        if (Pages.Count <= 0)
        {
            Console.WriteLine($"nextpage book has zero pages");
            return false;
        }
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
        if (Pages.Count <= 0)
        {
            Console.WriteLine($"prevpage book has zero pages");
            return false;
        }
        else
        {
            int newIndex = index - 1;
            if (newIndex < 0) return false;
            index = newIndex;
        }
        Pages[index].IsSelected = true;
        return true;
    }

    public bool FirstPage()
    {
        if (Pages.Count <= 0) return false;
        Pages.First().IsSelected = true;
        return true;
    }

    public bool LastPage()
    {
        if (Pages.Count <= 0) return false;
        Pages.Last().IsSelected = true;
        return true;
    }
    
    bool _ignoreWatcherEvents ;
    /// <summary>
    /// Rearrange pages LastModified values so that they are in the same order as
    /// their name when sorted by LastModified
    /// </summary>
    [RelayCommand]
    public async Task NameOrderToModifiedAndCreated()
    {
        var sortedModifieds = _pages
            .OrderBy(o => o.LastModified)
            .Select(o => o.LastModified)
            .ToList();
        var sortedCreateds = _pages
            .OrderBy(o => o.Created)
            .Select(o => o.Created)
            .ToList();
        var sorted = _pages
            .OrderBy(o => o.Name)
            .ToList();
        
        _ignoreWatcherEvents=true;
        
        for (int i = 0; i < _pages.Count; i++)
        {
            File.SetLastWriteTime(sorted[i].Path, sortedModifieds[i]);
            File.SetCreationTime(sorted[i].Path, sortedCreateds[i]);
            sorted[i].Model.Update(new FileInfo(sorted[i].Path));
        }

        await Task.Delay(1000);
        _ignoreWatcherEvents=false;
        Sort();
    }

    public bool CanNameOrderToModifiedAndCreated => Model is BookDirectory; 
    

    [RelayCommand]
    private async Task ModifiedOrderToName()
    {
        var sortedNames = _pages
            .OrderBy(o => o.Path)
            .Select(o => o.Path)
            .ToList();
        
        var sorted= _pages
            .OrderBy(o => o.LastModified)
            .ToList();
        
        _ignoreWatcherEvents=true;
        for (int i = 0; i < _pages.Count; i++)
        {
            File.Move(sorted[i].Path, sortedNames[i]);
            sorted[i].Model.Path = sortedNames[i];
        }

        Sort();
        await Task.Delay(1000);
        _ignoreWatcherEvents=false;
    }
    public bool CanModifiedOrderToName => Model is BookDirectory;

    [RelayCommand]private void ClearIVPCollection()
    {
        foreach (var page in Pages)
        {
            page.Ivp = null;
            page.IvpRectVisible = false;
        }
        Ivps?.Save(Model.IvpPath);
    }

    public void PreviewIvp(bool value)
    {
        foreach (var page in _pages)
        {
            if (value) page.ShowIvpRect();
            page.IvpRectVisible = value;
        }
    }
    
    #region FileSystemWatcher events

    public void FileSystemChanged(FileSystemEventArgs e)
    {
        RefreshModelInfo(Model.Path);
    }

    public void FileSystemRenamed(RenamedEventArgs e)
    {
        if (Path == e.OldFullPath)
            RefreshModelInfo(e.FullPath);
    }

    public void PageFileChanged(FileSystemEventArgs e)
    {
        if (_ignoreWatcherEvents) return;
        EnqueueRebuildPagesListJob();  
    } 
    public void PageFileRenamed(RenamedEventArgs e) {
        if (_ignoreWatcherEvents) return;
        EnqueueRebuildPagesListJob();  
    }

    void RefreshModelInfo(string newPath)
    {
        if (_ignoreWatcherEvents) return;
        Model.Path = newPath;

        if (Model is BookDirectory && Directory.Exists(newPath))
        {
            Model.Name = System.IO.Path.GetFileName(newPath.TrimEnd(System.IO.Path.DirectorySeparatorChar));
            Model.Created = Directory.GetCreationTime(newPath);
            Model.LastModified = Directory.GetLastWriteTime(newPath);
        }
        else if (File.Exists(newPath))
        {
            Model.Name = System.IO.Path.GetFileName(newPath);
            Model.Created = File.GetCreationTime(newPath);
            Model.LastModified = File.GetLastWriteTime(newPath);
        }
    }
    
    private int _pagesLoading;
    void EnqueueRebuildPagesListJob()
    {
        if (_ignoreWatcherEvents) return;
        if (Interlocked.CompareExchange(ref _pagesLoading, 1, 0) == 0)
        {
            _ = Task.Run(async () =>
            {
                bool success = false;
                try
                {
                    await Task.Delay(1000);
                    Task? loadPages = null;
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _pages.Clear();
                        loadPages = LoadPagesListAsync();
                    });

                    if (loadPages != null) await loadPages;
                    success = true;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }
                finally
                {
                    Interlocked.Exchange(ref _pagesLoading, 0);
                }

                if (success)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => Sort(CurrentSortOption, CurrentSortAscending));
                }
            });
        }
    }

    
    #endregion



}