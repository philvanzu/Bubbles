using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PDFiumSharp;

namespace Bubbles4.Models;

public class BookPdf : BookBase
{
    public BookPdf(string path, string name, int pageCount, DateTime lastModified, DateTime created)
        : base(path, name, lastModified, pageCount, created) { }

    public override string IvpPath =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? System.IO.Path.GetDirectoryName(Path)! + "\\.ivp"
            : System.IO.Path.GetDirectoryName(Path)! + "/.ivp";

    public override async Task LoadPagesList(Action<List<Page>> callback)
    {
        PagesListCts?.Cancel();
        PagesListCts?.Dispose();
        PagesListCts = new CancellationTokenSource();
        var token = PagesListCts.Token;

        await FileIOThrottler.WaitAsync(token);
        List<Page> pages = new();
        try
        {
            using var doc = new PdfDocument(Path);
            PageCount = doc.Pages.Count; 
            for (int i = 0; i < PageCount; i++)
            {
                var p = new Page
                {
                    Path = $"{Path}/page_{i}",
                    Name = $"page_{i}",
                    Index = i,
                    Created = Created,
                    LastModified = LastModified
                };
                pages.Add(p);
                PagesCts.TryAdd(p.Path, null);

            }
        }
        catch (TaskCanceledException) { }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Console.WriteLine(ex); }
        finally
        {
            FileIOThrottler.Release(); 
        }

        callback(pages);
    }


    private async Task DispatchThumbnailAsync(int index, Action<Bitmap> callback, CancellationToken token)
    {
        await FileIOThrottler.WaitAsync(token);
        try
        {
            using var doc = new PdfDocument(Path);
            using var pdfPage = doc.Pages[index];

            int thumbWidth = 150;
            int thumbHeight = (int)(pdfPage.Height / pdfPage.Width * thumbWidth);

            using var bitmap = new PDFiumBitmap(thumbWidth, thumbHeight, true);
            pdfPage.Render(bitmap);

            using var ms = bitmap.AsBmpStream();
            var avaloniaBitmap = new Bitmap(ms);

            await Dispatcher.UIThread.InvokeAsync(() => callback(avaloniaBitmap));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
        finally
        {
            FileIOThrottler.Release();
        }
    }

    public override async Task LoadThumbnailAsync(Action<Bitmap> callback)
    {
        ThumbnailCts?.Cancel();
        ThumbnailCts?.Dispose();
        ThumbnailCts = new CancellationTokenSource();
        var token = ThumbnailCts.Token;

        try
        {
            await DispatchThumbnailAsync(0, callback, token);
        }
        catch (TaskCanceledException) { }
        catch (Exception ex) { Console.WriteLine(ex); }
        finally
        {
            ThumbnailCts?.Dispose();
            ThumbnailCts = null;
        }
    }

    public override async Task LoadThumbnailAsync(Action<Bitmap> callback, string key)
    {
        if (!PagesCts.ContainsKey(key))
            throw new ArgumentException("Invalid page path in BookPdf.LoadThumbnailAsync");

        PagesCts[key]?.Cancel();
        PagesCts[key]?.Dispose();
        PagesCts[key] = new CancellationTokenSource();
        var token = PagesCts[key]!.Token;

        //bit of not optimally design OO awkwardness here
        int index;
        int underscoreIndex = key.LastIndexOf('_');
        if (underscoreIndex >= 0 && int.TryParse(key.Substring(underscoreIndex + 1), out index))
        {  
            try
            {
                await DispatchThumbnailAsync(index, callback, token);
            }
            catch (Exception ex) { Console.WriteLine(ex); }
            finally
            {
                PagesCts[key]?.Dispose();
                PagesCts[key] = null;
            }
        }
    }

    public override async Task LoadFullImageAsync(Page page, Action<Bitmap?> callback, CancellationToken token)
    {
        Bitmap? bmp = null;
        await FileIOThrottler.WaitAsync(token);
        token.ThrowIfCancellationRequested();
        try
        {
            using var doc = new PdfDocument(Path);
            using var pdfPage = doc.Pages[page.Index];

            int width = (int)(pdfPage.Width * 2);
            int height = (int)(pdfPage.Height * 2);

            using var bitmap = new PDFiumBitmap(width, height, true);
            pdfPage.Render(bitmap);

            using var ms = bitmap.AsBmpStream();
            bmp = new Bitmap(ms);
            await Dispatcher.UIThread.InvokeAsync(() => callback(bmp));
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
                Console.WriteLine($"Full image load failed: {ex}");
            bmp?.Dispose();
            callback(null);
        }
        finally
        {
            FileIOThrottler.Release();
        }
    }
}
