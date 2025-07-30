using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Bubbles4.Services;
using Bubbles4.ViewModels;

namespace Bubbles4.Models;

public class BookDirectory:BookBase
{
    public BookDirectory (string path, string name, int pageCount, DateTime modified, DateTime created)
        :base(path, name, modified, pageCount, created)
    {
    }
    
    string? _thumbnailPath;

    void FindThumbnailPath()
    {
        var files = Directory.EnumerateFileSystemEntries(Path);
        foreach (var filePath in files)
        {
            if (FileAssessor.IsImage(filePath))
            {
                _thumbnailPath = filePath;
                break;
            }
        }
    }

    void AltFindThumbnailPath()
    {
        DirectoryInfo dir = new DirectoryInfo(Path);
        var files = dir.GetFiles();
        foreach (var file in files)
        {
            if (FileAssessor.IsImage(file.FullName))
            {
                _thumbnailPath = file.FullName;
                break;
            }
        }
    }
    
    public override async Task<Bitmap?> LoadThumbnailAsync()
    {
        
        if (string.IsNullOrEmpty(_thumbnailPath))
        {
            FindThumbnailPath();
            if (string.IsNullOrEmpty(_thumbnailPath))
            {
                await Task.Delay(1000);
                AltFindThumbnailPath();
                if (string.IsNullOrEmpty(_thumbnailPath))
                {
                    Console.WriteLine($"No thumbnail path found for {Path}");
                    return null;    
                }
            }
        }
        
        ThumbnailCts?.Cancel();
        ThumbnailCts?.Dispose();
        ThumbnailCts = new CancellationTokenSource();
        var token = ThumbnailCts.Token;
        
        try
        {
            await FileIOThrottler.WaitAsync(token); // respects cancellation
            token.ThrowIfCancellationRequested();
            var thmb = await Task.Run(()=>
            {
                token.ThrowIfCancellationRequested();
                return ImageLoader.LoadImage(_thumbnailPath, ImageLoader.ThumbMaxSize, token);
            }, token);
            return thmb?.Item1;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Console.WriteLine(ex.ToString()); }
        finally
        {
            FileIOThrottler.Release();
            ThumbnailCts?.Dispose();
            ThumbnailCts = null;
        }
        return null;
    }
    public override async Task<(Bitmap?, PixelSize?)?> LoadThumbnailAsync(string key)
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
            return await Task.Run(()=>ImageLoader.LoadImage(key, ImageLoader.ThumbMaxSize, token), token);
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
                List<Page> pages = new();
                try
                {
                    if (!Directory.Exists(Path)) return pages; 
                    
                    DirectoryInfo info = new DirectoryInfo(Path);
                    int index = 0;
                    foreach (FileInfo file in info.GetFiles())
                    {
                        if (FileAssessor.IsImage(file.FullName))
                        {
                            pages.Add(new Page()
                            {
                                Path = file.FullName, Name = file.Name, Index = index++, Created = file.CreationTime,
                                Modified = file.LastWriteTime
                            });
                        }
                    }
                }
                catch(Exception ex){Console.Error.WriteLine(ex);}
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
    public override async Task SaveCroppedIvpToSizeAsync(PageViewModel page, string path, Rect? cropRect, int maxSize)
    {
        if (!File.Exists(page.Path)) return;
        DateTime created = File.GetCreationTime(page.Path);
        DateTime lastModified = File.GetLastWriteTime(page.Path);
        try
        {
            using var inputStream = File.OpenRead(page.Path);
            using var resizedStream = await Task.Run(() => ImageLoader.DecodeCropImage(inputStream, maxSize, cropRect));

            if (resizedStream != null)
            {
                await FileIOThrottler.WaitAsync();
                try
                {
                    using var fileStream = File.Create(path);

                    if (resizedStream.CanSeek)
                        resizedStream.Seek(0, SeekOrigin.Begin);

                    await resizedStream.CopyToAsync(fileStream);
                    await fileStream.FlushAsync();
                    File.SetCreationTime(path, created);
                    File.SetLastWriteTime(path, lastModified);
                }
                finally
                {
                    FileIOThrottler.Release();
                }
            }
        }
        catch (Exception e){ Console.WriteLine($"Could not decode image file : {e}"); }
    }
    
    public override string MetaDataPath =>
        Path + (Path.EndsWith(System.IO.Path.DirectorySeparatorChar) ? "" : System.IO.Path.DirectorySeparatorChar);

    public override void RenameFile(string newName)
    {
        try
        {
            var parentfolder = System.IO.Path.GetDirectoryName(Path);
            
            if (parentfolder == null)
                throw new DirectoryNotFoundException();

            var newPath = System.IO.Path.Combine(parentfolder, newName);
            Directory.Move(Path, newPath);   
        }
        catch(Exception e){Console.Error.WriteLine(e);}
    }
}