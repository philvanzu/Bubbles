using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Bubbles4.Models;

namespace Bubbles4.ViewModels;

public class BookViewModel: ViewModelBase
{
    private BookBase _bookBase;
    
    public string Path => _bookBase.Path;
    public string Name => _bookBase.Name;
    public int PageCount => _bookBase.PageCount;
    public DateTime LastModified => _bookBase.LastModified;
    public DateTime Created => _bookBase.Created;

    Bitmap? _thumbnail;
    public Bitmap? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value);
    }
    private bool _isThumbnailLoading;
    

    public BookViewModel(BookBase bookBase)
    {
        this._bookBase = bookBase;
    }
    
    

    public async Task LoadThumbnailAsync()
    {   
        if (_thumbnail != null || _isThumbnailLoading)
            return;

        _isThumbnailLoading = true;
        try
        {
            await _bookBase.LoadThumbnailAsync(bitmap => Thumbnail = bitmap );
        }
        finally
        {
            _isThumbnailLoading = false;
        }
    }

    public override async Task OnAttach()
    {
        //Console.WriteLine("Attach :" +this.Name);
        await LoadThumbnailAsync();
    }

    public override async Task OnDetach()
    {
        Console.WriteLine("Detach :" +this.Name);
        _thumbnail?.Dispose();
        _thumbnail = null;
        OnPropertyChanged(nameof(Thumbnail));
        await base.OnDetach();
        
    }
}