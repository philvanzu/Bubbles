using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
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
    private Bitmap? _thumbnail;
     
    public Bitmap? Thumbnail
    {
        get => _thumbnail;
        set
        {
            SetProperty(ref _thumbnail, value);
            /*
            if(value == null)Console.WriteLine($"Thmb null for {Path}");
            else Console.WriteLine($"Thmb for {Path} {_thumbnail.PixelSize.Width}x{_thumbnail.PixelSize.Height}");
            */
        } 
    }

    public ImageViewingParams? Ivp
    {
        get => Book.Ivps?.Get(Name);
        set
        {
            value.filename = Name;
            Book.Ivps?.AddOrUpdate(value);
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
    [RelayCommand] public void PointerPressed()=>IsSelected = true;
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

    
    [RelayCommand]
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
    private bool CanDelete => Book.Model is BookDirectory;
    [ObservableProperty] bool _isSelected;
    

    
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
    public async Task UnLoadAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => Unload());
    }
    public async Task LoadThumbnailAsync()
    {
        await Book.PreparePageThumbnailAsync(this);
    }
    

    
}