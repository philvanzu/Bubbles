using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Bubbles4.Services;

namespace Bubbles4.Models;

public class BookDirectory:BookBase
{
    public BookDirectory (string path, string name, int pageCount, DateTime lastModified, DateTime created)
        :base(path, name, lastModified, pageCount, created)
    {
    }
    
    string? _thumbnailPath;

    void FindThumbnailPath()
    {
        foreach (var filePath in Directory.EnumerateFileSystemEntries(Path))
        {
            if (FileTypes.IsImage(filePath))
            {
                _thumbnailPath = filePath;
                break;
            }
        }
    }
    
    
    public override async Task LoadThumbnailAsync(Action<Bitmap?> callback)
    {
        
        if (string.IsNullOrEmpty(_thumbnailPath))
        {
            FindThumbnailPath();
            if (string.IsNullOrEmpty(_thumbnailPath)) return;
        }
        
        ThumbnailCts?.Cancel();
        ThumbnailCts?.Dispose();
        ThumbnailCts = new CancellationTokenSource();
        
        
        try {
            await FileIOThrottler.WaitAsync(ThumbnailCts.Token); // respects cancellation

            var thmb = await Task.Run(()=>ThumbnailService.LoadThumbnail(_thumbnailPath, 240), ThumbnailCts.Token);
            FileIOThrottler.Release();
            await Dispatcher.UIThread.InvokeAsync(() => { callback(thmb); });
        }
        catch (OperationCanceledException)
        {
            
        }
        catch (Exception ex) {Console.WriteLine(ex.ToString());}
    }
    public override async Task LoadThumbnailAsync(Action<Bitmap?> callback, string key)
    {
        if(!PagesCts.ContainsKey(key)) 
            throw new ArgumentException($"Path {key} is not a valid PageCts key");
        
        PagesCts[key]?.Cancel();
        PagesCts[key]?.Dispose();
        PagesCts[key] = new CancellationTokenSource();
        var token = PagesCts[key].Token;
        
        Bitmap? thmb = null;
        try
        {
            await FileIOThrottler.WaitAsync(token); // respects cancellation
            token.ThrowIfCancellationRequested();
            thmb = await Task.Run(() => ThumbnailService.LoadThumbnail(key, 240), token);
            FileIOThrottler.Release();
            await Dispatcher.UIThread.InvokeAsync(() => { callback(thmb); });
        }
        catch (OperationCanceledException)
        {
        }
        catch (KeyNotFoundException)
        {
            if (thmb != null)
            {
                thmb.Dispose();
            }
        }
        finally
        {
            if (PagesCts.ContainsKey(key))
            {
                PagesCts[key]?.Dispose();
                PagesCts[key] = null;
            }
        }
    }
    public override async Task LoadPagesList(Action<List<Page>> callback)
    {
        PagesListCts?.Cancel();
        PagesListCts?.Dispose();
        PagesListCts = new CancellationTokenSource();
        try
        {
            var pages = await Task.Run(() =>
            {
                DirectoryInfo info = new DirectoryInfo(Path);
                List<Page> pages = new();
                int index = 0;
                foreach (FileInfo file in info.GetFiles())
                {
                    if (FileTypes.IsImage(file.FullName))
                    {
                        pages.Add(new Page()
                        {
                            Path = file.FullName, Name = file.Name, Index = index++, Created = file.CreationTime,
                            LastModified = file.LastWriteTime
                        });
                    }
                }

                return pages;
            }, PagesListCts.Token);
            await Dispatcher.UIThread.InvokeAsync(() => { callback(pages); });
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            PagesListCts?.Dispose();
            PagesListCts = null;
        }
    }
    
    public override async Task LoadFullImageAsync(Page page, Action<Bitmap?> callback, CancellationToken token)
    {
        if (!File.Exists(page.Path)) 
        {
            callback(null);
            return;
        }

        Bitmap? bmp = null;
        try
        {
            await FileIOThrottler.WaitAsync(token);
            token.ThrowIfCancellationRequested();
            try
            {
                bmp = await Task.Run(() => new Bitmap(page.Path), token);
            }
            catch (OperationCanceledException) { }
            catch (Exception e){ Console.WriteLine($"Could not decode image file : {e}"); }
            finally { FileIOThrottler.Release(); }

            await Dispatcher.UIThread.InvokeAsync(() => callback(bmp));
        }
        catch (OperationCanceledException){}
        catch (Exception e)
        {
            Console.WriteLine(e);
            bmp?.Dispose();
        }
    }
    public override string IvpPath 
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Path + "\\.ivp";
            return Path + "/.ivp";
        }
        
    } 
}