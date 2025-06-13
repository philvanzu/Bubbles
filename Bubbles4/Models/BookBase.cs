using System;
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

    public abstract Task LoadThumbnailAsync(Action<Bitmap> callback);
    

}