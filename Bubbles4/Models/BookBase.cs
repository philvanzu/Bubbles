using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Media.Imaging;
using Bubbles4.ViewModels;

namespace Bubbles4.Models;

public abstract class BookBase
{
    public string Path;
    public string Name;
    public int PageCount;
    public DateTime Modified;
    public DateTime Created;
    
    
    protected BookBase (string path, string name, DateTime modified, int pageCount, DateTime created)
    {
        this.Path = path;
        this.Name = name;
        this.Modified = modified;
        this.PageCount = pageCount;
        this.Created = created;
    }
    
    public static readonly SemaphoreSlim FileIOThrottler = new(8) ;
    protected CancellationTokenSource? ThumbnailCts;
    public ConcurrentDictionary<string, CancellationTokenSource?> PagesCts = new();
    
    public abstract Task<Bitmap?> LoadThumbnailAsync();
    public abstract Task<(Bitmap?, PixelSize?)?> LoadThumbnailAsync(string key);
    public abstract Task<Bitmap?> LoadFullImageAsync(Page page, CancellationToken token);

    public void CancelThumbnailLoad()
    {
        ThumbnailCts?.Cancel();
        //ThumbnailCts?.Dispose();
        //ThumbnailCts = null;
    }

    protected CancellationTokenSource? PagesListCts;
    public abstract Task<List<Page>?> LoadPagesList();
    public virtual void UnloadPagesList() { }

    public void CancelPagesListLoad()
    {
        PagesListCts?.Cancel();
        PagesListCts?.Dispose();
        PagesListCts = null;
    }
    public abstract string MetaDataPath { get; }
    public string IvpPath => MetaDataPath + ".ivp";
    public string? ParentPath => System.IO.Path.GetDirectoryName(Path);

    public string Serialize()
    {
        BookInfo.Types t  = BookInfo.Types.dir;
        if (this is BookArchive)  t = BookInfo.Types.archive;
        else if ( this is BookPdf) t = BookInfo.Types.pdf;

        var info = new BookInfo()
        {
            Path = this.Path,
            Name = this.Name,
            Created = this.Created,
            Modified = this.Modified,
            Type = t
        };
        var json = JsonSerializer.Serialize(info);
        return json;
    }

    public static BookBase? Deserialize(string json)
    {
        var info = JsonSerializer.Deserialize<BookInfo>(json);
        if (info != null)
        {
            switch (info.Type)
            {
                case BookInfo.Types.dir:
                    return new BookDirectory(info.Path, info.Name, -1, info.Modified, info.Created);
                case BookInfo.Types.pdf:
                    return new BookPdf(info.Path, info.Name, -1, info.Modified, info.Created);
                case BookInfo.Types.archive:
                    return new BookArchive(info.Path, info.Name, -1, info.Modified, info.Created);
            }
        }
        return null;
    }

    public abstract Task SaveCroppedIvpToSizeAsync(PageViewModel page, string path, Rect? cropRect, int maxSize, CancellationToken cancellationToken);

    public virtual void RenameFile(string newName)
    {
        try
        {
            var ext = System.IO.Path.GetExtension(Path);
            var folder = ParentPath;
            if (folder != null)
            {
                var newPath = System.IO.Path.Combine(folder, newName);
                var newExt = System.IO.Path.GetExtension(newPath);
                if (ext != newExt) newPath += ext;
                File.Move(Path, newPath);    
            }
        }
        catch(Exception e){Console.Error.WriteLine(e);}
    }
}

public class BookInfo
{
    public enum Types {dir, archive, pdf};
    public Types Type { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime Modified { get; set; }
    public DateTime Created { get; set; }
}