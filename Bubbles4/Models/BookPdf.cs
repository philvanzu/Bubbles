using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace Bubbles4.Models;

public class BookPdf:BookBase
{
    public BookPdf (string? path, string name, int pageCount, DateTime lastModified, DateTime created)
        :base(path, name, lastModified, pageCount, created)
    {
    }

    public override async Task LoadThumbnailAsync(Action<Bitmap> callback)
    {
        await Task.CompletedTask;
    }

    public override async Task LoadThumbnailAsync(Action<Bitmap> callback, string key )
    {
        await Task.CompletedTask;
    }

    public override async Task LoadFullImageAsync(Page page, Action<Bitmap?> callback, CancellationToken token)
    {
        await Task.CompletedTask;
    }

    public override async Task LoadPagesList(Action<List<Page>> callback)
    {
        await Task.CompletedTask;
    }
    public override string IvpPath
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return System.IO.Path.GetDirectoryName(Path)! + "\\.ivp";
            return System.IO.Path.GetDirectoryName(Path)! + "/.ivp";
        }
    } 
}