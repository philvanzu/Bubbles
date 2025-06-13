using System;
using System.IO;
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
    public override async Task LoadThumbnailAsync(Action<Bitmap> callback)
    {
        if (string.IsNullOrEmpty(_thumbnailPath))
        {
            FindThumbnailPath();
            if (string.IsNullOrEmpty(_thumbnailPath)) return;
        }
        var thmb = await Task.Run(()=>ThumbnailService.LoadThumbnail(_thumbnailPath, 240));
        await Dispatcher.UIThread.InvokeAsync(() => { callback(thmb); });

    }
}