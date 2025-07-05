using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
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

public partial class BookViewModel: ViewModelBase
{
    private LibraryViewModel _library;
    private BookBase _model;
    public BookBase Model => _model;
    
    public string Path => _model.Path;
    public string Name => _model.Name;
    public int PageCount => _pages?.Count??_model.PageCount;
    public DateTime LastModified => _model.LastModified;
    public DateTime Created => _model.Created;
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
    
    List<PageViewModel> _pages = new ();
    ObservableCollection<PageViewModel> _pagesMutable = new ();
    public ReadOnlyObservableCollection<PageViewModel> Pages { get; }
    
    private LibraryConfig.SortOptions _currentSortOption;
    private bool _currentSortAscending; //ascending
    public BookViewModel(BookBase book, LibraryViewModel library, MainViewModel mainViewModel)
    {
        this._mainViewModel = mainViewModel;
        this._model = book;
        this._library = library;
        
        Pages = new ReadOnlyObservableCollection<PageViewModel>(_pagesMutable);
        _currentSortOption = _mainViewModel.Config?.BookSortOption ?? LibraryConfig.SortOptions.Path;
        _currentSortAscending = _mainViewModel.Config?.BookSortAscending ?? true;
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

            LibraryConfig.SortOptions.Random => RandomizeAndReturnComparer(),

            _ => SortExpressionComparer<PageViewModel>.Ascending(x => x.Name)
        };
    }
    private IComparer<PageViewModel> RandomizeAndReturnComparer()
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

        return Comparer<PageViewModel>.Create((x, y) =>
        {
            int xHash = Hash(x.Path) ^ seed;
            int yHash = Hash(y.Path) ^ seed;
            return xHash.CompareTo(yHash);
        });
    }
    public void Sort(LibraryConfig.SortOptions? sort=null, bool? ascending=null)
    {
        if(sort == null) sort  = _mainViewModel.Config?.BookSortOption ?? LibraryConfig.SortOptions.Path;
        if(ascending == null) ascending = _mainViewModel.Config?.BookSortAscending ?? true;
        _currentSortOption = sort.Value;
        _currentSortAscending = ascending.Value;
        
        var comparer = GetComparer(_currentSortOption, _currentSortAscending);
        var sorted = _pages.OrderBy(p => p, comparer);

        _pagesMutable.Clear();
        _pagesMutable.AddRange(sorted);

        OnPropertyChanged(nameof(Pages));
    }

    public void ReverseSortOrder()
    {
        _currentSortAscending = !_currentSortAscending;
        Sort(_currentSortOption, _currentSortAscending);
    }
    
    public async Task PrepareThumbnailAsync()
    {
        if (_isThumbnailLoading) return;
        


        _isThumbnailLoading = true;
        try
        {
            //Console.WriteLine($"awaiting thumbnail from BookViewModel {Path}");
            await _model.LoadThumbnailAsync(bitmap =>
            {
                if (_thumbnail != null)
                {
                    var oldBitmap = _thumbnail;
                    _thumbnail = bitmap;
                    OnPropertyChanged(nameof(Thumbnail));

                    if (oldBitmap != null && !ReferenceEquals(oldBitmap, bitmap))
                    {
                        oldBitmap.Dispose();
                    }
                }
                else Thumbnail = bitmap;
                OnPropertyChanged(nameof(Thumbnail));
                
                //Todo: remove this log
                if(_library.GetBookIndex(this) == 0)
                    Console.WriteLine($"Thumbnail set: {_thumbnail != null}");
            });
        }
        catch (Exception ex){Console.WriteLine(ex);}
        finally
        {
            _isThumbnailLoading = false;
        }
    }
    public async Task ClearThumbnailAsync()
    {

        //Console.WriteLine($"unloading thmbnail idx {} at :{Name}");
        if (_isThumbnailLoading) _model.CancelThumbnailLoad();
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
            await _model.LoadThumbnailAsync(bitmap => page.Thumbnail = bitmap, page.Path);

        }
        finally
        {
            page.IsThumbnailLoading = false;
        }
    }
    
    public async Task LoadPagesListAsync()
    {
        await _model.LoadPagesList(pgs =>
        {
            if (pgs != null)
            {
                _pages.Clear();
                foreach (var page in pgs)
                {
                    _pages.Add(new PageViewModel(this, page));
                    _model.PagesCts.TryAdd(page.Path, null);
                }
                Sort();
                Model.PageCount = _pages.Count;
                
                ImageViewingParamsCollection = IvpCollection.Load(_model.IvpPath);
                if (ImageViewingParamsCollection == null && _mainViewModel.Config?.UseIVPs == true)
                {
                    ImageViewingParamsCollection = new();
                }
                    
            }
        });
        
        
    }

    public void UnloadPagesList()
    {
        _model.CancelPagesListLoad();
        if (ImageViewingParamsCollection != null && _mainViewModel.Config?.UseIVPs == true)
        {
            ImageViewingParamsCollection.Save(_model.IvpPath);
            ImageViewingParamsCollection = null;
        }
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
                if (kv.Value != null)
                {
                    kv.Value.Cancel();
                    kv.Value.Dispose();
                }
            }
            _model.PagesCts.Clear();
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
    private async Task Delete()
    {
        var dialog = new OkCancelViewModel
        {
            Content = $"Do you want to delete [{Path}] permanently?"
        };
        var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if (window != null)
        {
            var result = await _mainViewModel.DialogService.ShowDialogAsync<bool>(window, dialog);
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

    public int GetPageIndex(PageViewModel? pageViewModel)
    {
        if(pageViewModel == null)return -1;
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
    
    bool _ignoreWatcherEvents = false;
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
    
    private int _pagesLoading = 0;
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
                    await Dispatcher.UIThread.InvokeAsync(() => Sort(_currentSortOption, _currentSortAscending));
                }
            });
        }
    }

    
    #endregion

}