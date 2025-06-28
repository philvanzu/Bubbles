using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Bubbles4.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bubbles4.ViewModels;

public partial class PageViewModel:ViewModelBase
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

    private ImageViewingParams? _ivp;
    public ImageViewingParams? Ivp
    {
        get
        {
            if (_ivp == null) _ivp = Book.ImageViewingParamsCollection?.Get(Name);
            if(_ivp != null && !_ivp.IsValid) _ivp = null;
            return _ivp; 
        }
        set
        {
            SetProperty(ref _ivp, value);
            if(value != null && value.IsValid) Book.ImageViewingParamsCollection?.Update(value);
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
    
    [RelayCommand] public void PointerPressed()=>IsSelected = true;
    [RelayCommand] private void DeleteCommand(){}
    [RelayCommand] private void ShowDetailsCommand(){}
    
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
        Unload();
        await Task.CompletedTask;
    }
    public async Task LoadThumbnailAsync()
    {
        await Book.PreparePageThumbnailAsync(this);
    }
    

    
}