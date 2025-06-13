using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace Bubbles4.Models;

public class BookArchive:BookBase
{
    public BookArchive (string path, string name, int pageCount, DateTime lastModified, DateTime created)
        :base(path, name, lastModified, pageCount, created)
    {
    }

    public override async Task LoadThumbnailAsync(Action<Bitmap> callback)
    {
        throw new NotImplementedException();
    }
}