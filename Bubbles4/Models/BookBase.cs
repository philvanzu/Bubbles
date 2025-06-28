using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace Bubbles4.Models;

public abstract class BookBase
{
    public string Path;
    public string Name;
    public int PageCount;
    public DateTime LastModified;
    public DateTime Created;
    
    
    protected BookBase (string path, string name, DateTime lastModified, int pageCount, DateTime created)
    {
        this.Path = path;
        this.Name = name;
        this.LastModified = lastModified;
        this.PageCount = pageCount;
        this.Created = created;
    }
    
    public static readonly SemaphoreSlim FileIOThrottler = new(1) ;
    protected CancellationTokenSource? ThumbnailCts;
    public ConcurrentDictionary<string, CancellationTokenSource?> PagesCts = new();
    
    public abstract Task LoadThumbnailAsync(Action<Bitmap?> callback);
    public abstract Task LoadThumbnailAsync(Action<Bitmap?> callback, string key);
    public abstract Task LoadFullImageAsync(Page page, Action<Bitmap?> callback, CancellationToken token);

    public void CancelThumbnailLoad()
    {
        ThumbnailCts?.Cancel();
        ThumbnailCts?.Dispose();
        ThumbnailCts = null;
    }

    protected CancellationTokenSource? PagesListCts;
    public abstract Task LoadPagesList(Action<List<Page>?> callback);
    public virtual void UnloadPagesList() { }

    public void CancelPagesListLoad()
    {
        PagesListCts?.Cancel();
        PagesListCts?.Dispose();
        PagesListCts = null;
    }
    public abstract string IvpPath { get; }
}