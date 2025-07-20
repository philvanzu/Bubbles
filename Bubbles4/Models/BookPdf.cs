using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Bubbles4.Services;
using Bubbles4.ViewModels;
using PDFiumSharp;

namespace Bubbles4.Models;

public class BookPdf : BookBase
{
    private new static readonly SemaphoreSlim FileIOThrottler = new SemaphoreSlim(1);
    public BookPdf(string path, string name, int pageCount, DateTime lastModified, DateTime created)
        : base(path, name, lastModified, pageCount, created) { }

    public override string MetaDataPath => 
        System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!, 
            System.IO.Path.GetFileNameWithoutExtension(Path));



    public override async Task<List<Page>?> LoadPagesList()
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
            }
        }
        catch (TaskCanceledException) { }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Console.WriteLine(ex); }
        finally
        {
            FileIOThrottler.Release(); 
        }

        return pages;
    }


    private async Task<(Bitmap?, PixelSize?)?> DispatchThumbnailAsync(int index, CancellationToken token)
    {
        await FileIOThrottler.WaitAsync(token);
        try
        {
            using var doc = new PdfDocument(Path);
            using var pdfPage = doc.Pages[index];

            var targetDimensions = 
                ImageLoader.GetTargetDimensions((int)pdfPage.Width, (int)pdfPage.Height, ImageLoader.ThumbMaxSize);

            using var bitmap = new PDFiumBitmap(targetDimensions.width, targetDimensions.height, true);
            pdfPage.Render(bitmap);

            using var ms = bitmap.AsBmpStream();
            var avaloniaBitmap = new Bitmap(ms);

            return (avaloniaBitmap, new PixelSize(targetDimensions.width, targetDimensions.height));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
        finally
        {
            FileIOThrottler.Release();
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
            var tuple = await DispatchThumbnailAsync(0, token);
            return tuple?.Item1;
        }
        catch (TaskCanceledException) { }
        catch (Exception ex) { Console.WriteLine(ex); }
        finally
        {
            ThumbnailCts?.Dispose();
            ThumbnailCts = null;
        }
        return null;
    }

    public override async Task<(Bitmap?, PixelSize?)?> LoadThumbnailAsync(string key)
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
                return await  DispatchThumbnailAsync(index, token);
            }
            catch (Exception ex) { Console.WriteLine(ex); }
            finally
            {
                if (PagesCts.ContainsKey(key))
                {
                    PagesCts[key]?.Dispose();
                    PagesCts[key] = null;    
                }
            }
        }

        return null;
    }

    public override async Task<Bitmap?> LoadFullImageAsync(Page page, CancellationToken token)
    {
        Bitmap? bmp = null;
        await FileIOThrottler.WaitAsync(token);
        token.ThrowIfCancellationRequested();
        try
        {
            using var doc = new PdfDocument(Path);
            using var pdfPage = doc.Pages[page.Index];
            var targetDimensions = ImageLoader.GetTargetDimensions((int)pdfPage.Width*8, (int)pdfPage.Height*8, ImageLoader.ImageMaxSize);

            using var bitmap = new PDFiumBitmap(targetDimensions.width, targetDimensions.height, true);
            pdfPage.Render(bitmap);

            using var ms = bitmap.AsBmpStream();
            bmp = new Bitmap(ms);
            return bmp;
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
                Console.WriteLine($"Full image load failed: {ex}");
            bmp?.Dispose();
        }
        finally
        {
            FileIOThrottler.Release();
        }

        return null;
    }
    
    public override async Task SaveCroppedIvpToSizeAsync(PageViewModel page, string path, Rect? cropRect, int maxSize)
    {
        Console.WriteLine($"Saving cropped image from PDF is not implemented : {path}");
    }
}
