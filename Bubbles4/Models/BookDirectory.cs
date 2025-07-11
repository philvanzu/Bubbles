using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
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
    
    
    public override async Task<Bitmap?> LoadThumbnailAsync()
    {
        
        if (string.IsNullOrEmpty(_thumbnailPath))
        {
            FindThumbnailPath();
            if (string.IsNullOrEmpty(_thumbnailPath)) return null;
        }
        
        ThumbnailCts?.Cancel();
        ThumbnailCts?.Dispose();
        ThumbnailCts = new CancellationTokenSource();


        try
        {
            await FileIOThrottler.WaitAsync(ThumbnailCts.Token); // respects cancellation
            var thmb = await Task.Run(()=>ImageLoader.LoadImage(_thumbnailPath, ImageLoader.ThumbMaxSize), ThumbnailCts.Token);
            return thmb;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Console.WriteLine(ex.ToString()); }
        finally
        {
            FileIOThrottler.Release();
        }
        return null;
    }
    public override async Task<Bitmap?> LoadThumbnailAsync(string key)
    {
        if (!PagesCts.ContainsKey(key))
        {
            throw new ArgumentException($"Path {key} is not a valid PageCts key");

        } 
            
        
        PagesCts[key]?.Cancel();
        PagesCts[key]?.Dispose();
        PagesCts[key] = new CancellationTokenSource();
        var token = PagesCts[key]!.Token;
        
        Bitmap? thmb = null;
        try
        {
            await FileIOThrottler.WaitAsync(token); // respects cancellation
            return await Task.Run(()=>ImageLoader.LoadImage(key, ImageLoader.ThumbMaxSize), token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            if (thmb != null) thmb.Dispose();
        }
        finally
        {
            FileIOThrottler.Release();
            if (PagesCts.ContainsKey(key))
            {
                PagesCts[key]?.Dispose();
                PagesCts[key] = null;
            }
        }

        return null;
    }
    public override async Task<List<Page>?> LoadPagesList()
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
            return pages;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            PagesListCts?.Dispose();
            PagesListCts = null;
        }

        return null;
    }
    
    public override async Task<Bitmap?> LoadFullImageAsync(Page page, CancellationToken token)
    {
        if (!File.Exists(page.Path)) 
        {
            return null;
        }

        try
        {
            await FileIOThrottler.WaitAsync(token);
            token.ThrowIfCancellationRequested();
            try
            {
                //return await Task.Run(()=>ImageLoader.LoadImage(page.Path, ImageLoader.ImageMaxSize), token);
                return new Bitmap(page.Path);
            }
            catch (OperationCanceledException) { }
            catch (Exception e){ Console.WriteLine($"Could not decode image file : {e}"); }
            finally { FileIOThrottler.Release(); }

        }
        catch (OperationCanceledException){}
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return null;
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