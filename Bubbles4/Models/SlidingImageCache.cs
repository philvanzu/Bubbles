using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Bubbles4.ViewModels;

namespace Bubbles4.Models;

public class SlidingImageCache : IDisposable
{
    private MainViewModel _mainViewModel;
    private BookViewModel? _currentBook;
    private PageViewModel? _currentPage;
    private const int _size = 2; // multiply by 2 and add one to get the actual cache size.
    private Dictionary<PageViewModel, Bitmap?> _cache = new();
    private PriorityQueue<PageViewModel, int> _loadQueue = new();
    private const int MaxConcurrentLoads = 4;
    
    public SlidingImageCache(MainViewModel mv)
    {
        _mainViewModel = mv;
        _mainViewModel.PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.SelectedBook):
                _currentBook = _mainViewModel.SelectedBook;            
                break;
            case nameof(MainViewModel.CurrentPageViewModel):
                _currentPage = _mainViewModel.CurrentPageViewModel;
                if (_currentPage != null) UpdateState();
                else _mainViewModel.CurrentViewerData = null;
                break;
        }
    }

    void UpdateState()
    {
        if (_mainViewModel.SelectedBook == null)
        {
            throw new InvalidOperationException("SelectedBook is null");
        }
        //compute current priorities
        int curid = _mainViewModel.SelectedBook.GetPageIndex(_currentPage!);
        Dictionary<PageViewModel, int> priorities = new();
        int priority = 0;
        priorities.Add(_currentPage!, priority++);
        int id = curid;
        for (int i = 0; i < _size; i++)
        {
            id = id + 1;
            if (id >= 0 && id < _currentBook!.Pages.Count)
                priorities.Add(_currentBook.Pages[id], priority++);
            else break;
        }

        id = curid;
        for (int i = 0; i < _size; i++)
        {
            id = id - 1;
            if (id >= 0 && id < _currentBook!.Pages.Count)
                priorities.Add(_currentBook.Pages[id], priority++);
            else break;
        }

        foreach (var k in _cache.Keys.ToList())
        {
            if (!priorities.ContainsKey(k)) 
            {
                //remove expired cache items 
                k.ImgLoadCts?.Cancel();  // Cancel any pending load
                k.ImgLoadCts?.Dispose();
                k.ImgLoadCts = null;
    
                if(_mainViewModel.CurrentViewerData?.Image != null
                   && _mainViewModel.CurrentViewerData.Image.Equals(_cache[k]))
                    _mainViewModel.CurrentViewerData = null;
                _cache[k]?.Dispose();
                
                _cache.Remove(k);       
            }
            //remove loaded items from the priorities
            else
            {
                priorities.Remove(k);
                if (_mainViewModel.CurrentPageViewModel == k && !k.IsImageLoading && _cache[k] != null)
                    _mainViewModel.CurrentViewerData = new ViewerData() { Page = k, Image = _cache[k] };
            } 
        }
        
        //rebuild the load queue
        _loadQueue.Clear();
        foreach (var kv in priorities)
            _loadQueue.Enqueue(kv.Key, kv.Value);
        
        //process the load queue and load the missing bitmaps.
        _ = ProcessLoadQueueAsync();
    }
    
    private async Task ProcessLoadQueueAsync()
    {
        List<Task> loadTasks = new();

        while (_loadQueue.Count > 0)
        {
            // Pull the next highest-priority page
            var next = _loadQueue.Dequeue();

            // If already cached or loading, skip
            if (_cache.ContainsKey(next) || next.IsImageLoading)
                continue;
            
            // Pre-allocate null entry in cache to reserve space
            _cache[next] = null;
            
            // Start image loading
            var task = LoadPageBitmapAsync(next);
            loadTasks.Add(task);

            // Wait if max concurrency is reached
            if (loadTasks.Count >= MaxConcurrentLoads)
            {
                await Task.WhenAny(loadTasks);
                loadTasks = loadTasks.Where(t => !t.IsCompleted).ToList();
            }
        }

        // Wait for remaining tasks to finish
        if(loadTasks.Count > 0)
            await Task.WhenAll(loadTasks);
    }
    
    public async Task LoadPageBitmapAsync(PageViewModel pg)
    {
        if (pg.IsImageLoading) return;
        pg.IsImageLoading = true;
        
        pg.ImgLoadCts?.Cancel();
        pg.ImgLoadCts?.Dispose();
        pg.ImgLoadCts = new CancellationTokenSource();
        var token = pg.ImgLoadCts.Token;
        try
        {
            // Load bitmap off UI thread
            await pg.Book.Model.LoadFullImageAsync(
                pg.Model, 
                bitmap =>
                {
                    if (bitmap != null && _cache.ContainsKey(pg))
                    {
                        _cache[pg] = bitmap;
                        if (pg == _mainViewModel.CurrentPageViewModel
                               && _mainViewModel.CurrentViewerData?.Image != bitmap)
                            _mainViewModel.CurrentViewerData = new ViewerData() { Page = pg, Image = bitmap };
                    }
                    else
                    {
                        bitmap?.Dispose();
                    }
                },
                token
            );
        }
        catch (OperationCanceledException)
        {
            // Expected, silently ignore
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load image: {ex.Message}");
        }
        finally
        {
            pg.IsImageLoading = false;
        }
    }

    public void Dispose()
    {
        _mainViewModel.CurrentViewerData = null;
        foreach (var kv in _cache)
            kv.Value?.Dispose();
        _cache.Clear();
        foreach (var page in _currentBook?.Pages ?? Enumerable.Empty<PageViewModel>())
        {
            page.ImgLoadCts?.Cancel();
            page.ImgLoadCts?.Dispose();
            page.ImgLoadCts = null;
        }

        _mainViewModel.PropertyChanged -= OnPropertyChanged;
    }

}