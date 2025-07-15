using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Bubbles4.Services;
using SharpCompress.Archives;

namespace Bubbles4.Models;

public class BookArchive : BookBase
{
    private string? _coverKey; 
    private Task<List<Page>?>? _pagesListTask;
    private readonly SemaphoreSlim _pagesListLock = new(1, 1);
    
    //new static readonly SemaphoreSlim FileIOThrottler = new(1) ;
    
    public BookArchive(string path, string name, int pageCount, DateTime lastModified, DateTime created)
        : base(path, name, lastModified, pageCount, created) { }


    
    private (IArchive archive, FileStream stream)? TryOpenArchive()
    {
        IArchive? archive = null;
        FileStream? stream = null;
        try
        {
            stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
            archive = ArchiveFactory.Open(stream);
            return (archive, stream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cannot open {Path} : {ex}");
            archive?.Dispose();
            stream?.Dispose();
            return null;
        }
        
    }

    private void CloseArchive(IArchive? archive, FileStream? stream)
    {
        try
        {
            archive?.Dispose();
            stream?.Dispose();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }
    public async Task<List<Page>?> GetPagesListAsync()
    {    
        List<Page>? pages = null;
        await FileIOThrottler.WaitAsync();
        IArchive? archive = null;
        FileStream? stream = null;
        try
        {
            var pair = TryOpenArchive();
            if (pair == null)
                throw (new FileLoadException($"Could not open archive file : {Path}"));
            
            (archive, stream) = pair.Value;

            pages = archive.Entries
                .Where(e => !e.IsDirectory && FileTypes.IsImage(System.IO.Path.GetExtension(e.Key)??""))
                .Select(entry => new Page()
                {
                    Name = System.IO.Path.GetFileName(entry.Key)!,
                    Path = entry.Key!,
                    //Size = entry.Size,
                    Created = entry.CreatedTime ?? Created,
                    LastModified = entry.LastModifiedTime ?? LastModified,
                    //LastAccessTime = _book.LastAccessTime
                })
                .OrderBy(p => p.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (pages.Count > 0)
            {
                _coverKey = pages[0].Path;
                PageCount = pages.Count;
            }
            
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
        finally
        {
            CloseArchive(archive, stream);
            FileIOThrottler.Release();
        }
        return pages;
    }
    private async Task EnsureCoverKeyInitialized(CancellationToken token)
    {
        await _pagesListLock.WaitAsync();
        try
        {
            if (_pagesListTask == null)
                _pagesListTask = Task.Run(()=>GetPagesListAsync(), token);

            await _pagesListTask;
        }
        finally { _pagesListLock.Release(); }
    }
    private MemoryStream? ExtractPage(IArchive archive, string key)
    {
        MemoryStream? stream = null;
        //Console.WriteLine($"[{_book.Path}] Starting extraction of {page.Path}");
        try
        {
            var entry = archive.Entries.FirstOrDefault(e => e.Key == key);
            if (entry == null)
            {
                Console.WriteLine($"[{Path}] Entry not found: {key}");
                return null;
            }

            stream = new MemoryStream((int)entry.Size);
            entry.WriteTo(stream);
            stream.Seek(0, SeekOrigin.Begin);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }

        return stream;
        //Console.WriteLine($"[{_book.Path}] Completed extraction of {page.Path}");
    }
    
    private async Task<Bitmap?> LoadPageThumbnail(CancellationToken ct, string key)
    {
        Stream? stream = null;
        try
        {
            await FileIOThrottler.WaitAsync(ct);
            IArchive? archive = null;
            FileStream? fstream = null;
            try
            {
                var pair = TryOpenArchive();
                if (pair == null) throw (new FileLoadException($"Could not open archive file : {Path}"));
                (archive, fstream) = pair.Value;
                stream = ExtractPage(archive, key);
            }
            catch (TaskCanceledException){}
            catch (OperationCanceledException) { }
            catch (Exception ex) { Console.WriteLine(ex.ToString()); }
            finally
            {
                CloseArchive(archive, fstream);
                FileIOThrottler.Release();
            }

            if (stream == null) Console.WriteLine($"null archived file stream - aborting : {Path + key}");
            else
            {
                var bitmap = await Task.Run<Bitmap?>(()=>ImageLoader.DecodeImage(stream, ImageLoader.ThumbMaxSize, ct), ct);
                //if (bitmap == null) Console.WriteLine($"null bitmap from non null stream - aborting : {Path + key}");
                return bitmap;
            } 

        }
        catch ( OperationCanceledException){}
        catch (Exception ex)
        {
            Console.WriteLine($"Thumbnail load failed: {ex}");
        }
        finally
        {
            stream?.Dispose();
        }

        return null;

    }
    
    #region public_methods
    
    public override async Task<List<Page>?> LoadPagesList()
    {

        PagesListCts?.Cancel();
        PagesListCts?.Dispose();
        PagesListCts = new CancellationTokenSource();
        var token = PagesListCts.Token;

        await _pagesListLock.WaitAsync();
        try
        {
            if (_pagesListTask == null)
            {
                token.ThrowIfCancellationRequested();
                _pagesListTask = Task.Run(() => GetPagesListAsync());
            }

            var pages = await _pagesListTask;
            if (pages != null)
                return pages;
        }
        catch (TaskCanceledException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
        finally
        {
            _pagesListLock.Release();
            PagesListCts?.Dispose();
            PagesListCts = null;
        }

        return null;
    }

    

    public override async Task<Bitmap?> LoadThumbnailAsync()
    {
        ThumbnailCts?.Cancel();
        ThumbnailCts?.Dispose();
        ThumbnailCts = new CancellationTokenSource();
        var token = ThumbnailCts.Token;
        
        try
        {
            await EnsureCoverKeyInitialized(token);
            if(!string.IsNullOrEmpty(_coverKey))
                return await LoadPageThumbnail(token, _coverKey);
        }
        catch (TaskCanceledException){}
        finally
        {
            ThumbnailCts?.Dispose();
            ThumbnailCts = null;
        }

        return null;
    }

    public override async Task<Bitmap?> LoadThumbnailAsync(string key)
    {
        //can't provide key argument without having a list of pages
        //no need to ensure _pagesListTask has completed, it's a given.

        if (!PagesCts.ContainsKey(key))
            throw new ArgumentException("Invalid page path in BookArchive.LoadThumnailAsync");
    
        PagesCts[key]?.Cancel();
        PagesCts[key]?.Dispose();
        PagesCts[key] = new CancellationTokenSource();    
        
        
        var token = PagesCts[key]!.Token;
        try
        {
            return await LoadPageThumbnail(token, key);
        }
        finally
        {
            try
            {
                if (PagesCts.ContainsKey(key))
                {
                    PagesCts[key]?.Dispose();
                    PagesCts[key] = null;
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.ToString()); }
        }
    }

    public override async Task<Bitmap?> LoadFullImageAsync(Page page, CancellationToken token)
    {
        var key = page.Path;
        
        Bitmap? bmp = null;
        Stream? stream = null;
        try
        {
            await FileIOThrottler.WaitAsync(token);
            token.ThrowIfCancellationRequested();
            IArchive? archive = null;
            FileStream? fstream = null;
            try
            {
                var pair = TryOpenArchive();
                if (pair == null) throw (new FileLoadException($"Could not open archive file : {Path}"));
                (archive, fstream) = pair.Value;

                stream = ExtractPage(archive, key);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                CloseArchive(archive, fstream);
                FileIOThrottler.Release();
            }

            if (stream != null)
            {
                //bmp = await Task.Run(() => ImageLoader.DecodeImage(stream,ImageLoader.ImageMaxSize), token);
                bmp = await Task.Run(() => new Bitmap(stream), token);
                return bmp;
            }
        }
        catch (Exception ex)
        {
            if (!(ex is OperationCanceledException))
                Console.WriteLine($"Thumbnail load failed: {ex}");
            bmp?.Dispose();
        }
        finally
        {
            stream?.Dispose(); 
        }

        return null;
    }

    public override string MetaDataPath =>
        System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), 
            System.IO.Path.GetFileNameWithoutExtension(Path));

    #endregion
}
