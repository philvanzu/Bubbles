using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Bubbles4.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bubbles4.ViewModels;

public partial class PageViewModel:ViewModelBase, ISelectableItem
{
    public BookViewModel Book { get; private set; }
    private Page _page;
    public Page Model => _page;
    public CancellationTokenSource? ImgLoadCts { get; set; }
    
    public bool IsFirst { get; set; }
    [ObservableProperty]private Bitmap? _thumbnail;

    public PixelSize? ImageSize { get; set; }
    [ObservableProperty] private Rect _ivpRect;
    [ObservableProperty] private double _ivpRectTop;
    [ObservableProperty] private double _ivpRectLeft;
    [ObservableProperty] private double _ivpRectWidth;
    [ObservableProperty] private double _ivpRectHeight;
    [ObservableProperty] private bool _ivpRectVisible;

    public ImageViewingParams? Ivp
    {
        get => Book.Ivps?.Get(Name);
        set
        {
            if (value != null)
            {
                value.filename = Name;
                Book.Ivps?.AddOrUpdate(value);
            }
            else Book.Ivps?.Remove(Name);
            //Console.WriteLine($"ivp saved: {Path}");
        }
    }
    
    public bool ThumbnailLoaded => Thumbnail != null;
    public bool IsThumbnailLoading { get; set; }
    public bool IsImageLoading { get; set; }
    
    public int Index => Book.GetPageIndex(this);
    public string Path => _page.Path;
    public string Name => _page.Name;
    public DateTime Created => _page.Created;
    public DateTime LastModified => _page.LastModified;
    public int RandomIndex {get; set;}

    ~PageViewModel()
    {
        Unload();
    }


    [RelayCommand] private void ListItemPointerPressed()=>IsSelected = true;
    [RelayCommand]
    private void OpenInExplorer()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Path}\"") { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Try with xdg-open (common across most Linux desktop environments)
                Process.Start(new ProcessStartInfo("xdg-open", $"\"{Path}\"") { UseShellExecute = true });
            }
            else
            {
                throw new PlatformNotSupportedException("Only Windows and Linux are supported.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to open path: {ex.Message}");
        }
    }

    
    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task Delete()
    {
        var dialog = new OkCancelViewModel
        {
            Content = $"Do you want to delete [{Path}] permanently?"
        };
        var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if (window != null)
        {
            var result = await Book.MainViewModel.DialogService.ShowDialogAsync<bool>(window, dialog);
            if (result)
            {
                try
                {
                    if (File.Exists(Path)) File.Delete(Path);
                    else Console.Error.WriteLine($"Path not found: {Path}");

                    // Wait a tick in case the OS needs to release file handles
                    await Task.Yield();
                }
                catch (Exception ex) { Console.Error.WriteLine($"Hard delete failed: {ex.Message}"); }
            }    
        }
    }
    private bool CanDelete() => Book.Model is BookDirectory;
    [ObservableProperty] bool _isSelected;

    public bool IsFirstPage => Book.GetPageIndex(this) == 0;
    
    public PageViewModel(BookViewModel book, Page page)
    {
        this.Book = book;
        this._page = page;
    }
    

    partial void OnIsSelectedChanged(bool oldValue, bool newValue)
    {
        if (newValue && Book.SelectedPage != this)
        {
            Book.SelectedPage = this;
        }
        else if (oldValue && Book.SelectedPage == this)
        {
            Book.SelectedPage = null;
        }
    }

    public void Unload()
    {
        Thumbnail?.Dispose();
        Thumbnail = null;
    }

    public void ShowIvpRect()
    {
        if (Ivp != null && Ivp.IsValid && ImageSize.HasValue)
        {
            var iw = ImageSize.Value.Width;
            var ih = ImageSize.Value.Height;
            const double borderSize = 128;
            
            double scale = (iw > ih)
                ? borderSize / iw
                : borderSize / ih;

            double canvasW = iw * scale;
            double canvasH = ih * scale;

            // Compute the scale from image pixels to canvas pixels
            var scaleX = canvasW / iw;
            var scaleY = canvasH / ih;

            // Size of the zoom viewport in full image pixels
            var viewportW = 1920 / Ivp.zoom;
            var viewportH = 1080 / Ivp.zoom;

            // Top-left of the viewport in image pixels
            var leftPx = Ivp.centerX - viewportW / 2;
            var topPx = Ivp.centerY - viewportH / 2;

            // Clamp within bounds (optional)
            leftPx = Math.Max(0, Math.Min(leftPx, iw - viewportW));
            topPx = Math.Max(0, Math.Min(topPx, ih - viewportH));

            // Convert to canvas coordinates
            IvpRectLeft = leftPx * scaleX;
            IvpRectTop = topPx * scaleY;
            IvpRectWidth = viewportW * scaleX;
            IvpRectHeight = viewportH * scaleY;
            IvpRect = new  Rect(IvpRectLeft, IvpRectTop, IvpRectWidth, IvpRectHeight);
            IvpRectVisible = true;
            
            return;
        }

        IvpRectVisible = false;
    }
    
    public Rect? GetIvpCropRect(PixelSize? viewportSize = null)
    {
        if (Ivp != null && Ivp.IsValid && ImageSize.HasValue)
        {
            var v = viewportSize ?? new PixelSize(1920, 1080);
            var iw = ImageSize.Value.Width;
            var ih = ImageSize.Value.Height;

            // Size of the zoom viewport in full image pixels
            var viewportW = v.Width / Ivp.zoom;
            var viewportH = v.Height / Ivp.zoom;

            // Top-left of the viewport in image pixels
            var left = Ivp.centerX - viewportW / 2;
            var top = Ivp.centerY - viewportH / 2;
            left = Math.Max(0, Math.Min(left, iw - viewportW));
            top = Math.Max(0, Math.Min(top, ih - viewportH));
            
            var width = Math.Min(viewportW, iw-left);
            var height = Math.Min(viewportH, ih-top);
            return new Rect(left, top, width, height);
        }
        
        return null;
    }

    
}