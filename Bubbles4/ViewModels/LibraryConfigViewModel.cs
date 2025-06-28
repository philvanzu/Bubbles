using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Bubbles4.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bubbles4.ViewModels;

public partial class LibraryConfigViewModel : ObservableObject
{
    private LibraryConfig _libraryConfig;
    
    public string Path => _libraryConfig.Path;


    private bool _recursive = true;
    public bool Recursive
    {
        get => _recursive;
        set
        {
            SetProperty(ref _recursive, value);
            _navtree = !value;
            OnPropertyChanged(nameof(Navtree));
        }
    }

    private bool _navtree;
    public bool Navtree
    {
        get => _navtree;
        set
        {
            SetProperty(ref _navtree, value);
            _recursive = !value;
            OnPropertyChanged(nameof(Recursive));
        }
    }
    
    private bool _viewer;
    public bool Viewer
    {
        get => _viewer;
        set
        {
            SetProperty(ref _viewer, value);
            _reader = !value;
            OnPropertyChanged(nameof(Reader));
            if (value)
            {
                UseIVPs = value;
                AnimateIVPs = value;    
            }
        }
    }
    private bool _reader;
    public bool Reader
    {
        get => _reader;
        set
        {
            SetProperty(ref _reader, value);
            _viewer = !value;
            OnPropertyChanged(nameof(Viewer));
            if (value)
            {
                UseIVPs = !value;
                AnimateIVPs = !value;    
            }
        }
    }
    
    [ObservableProperty]bool _useIVPs;
    [ObservableProperty]bool _animateIVPs;
    [ObservableProperty]int _showPagingInfo;
    [ObservableProperty]int _showPagingInfoFontSize;
    [ObservableProperty]int _showAlbumPath;
    [ObservableProperty]int _showAlbumPathFontSize;
    [ObservableProperty]int _showPageName;
    [ObservableProperty]int _showPageNameFontSize;
    [ObservableProperty]int _showImageSize;
    [ObservableProperty]int _showImageSizeFontSize;
    
    public LibraryConfigViewModel(LibraryConfig config)
    {
        Recursive = config.Recursive;

        Viewer = config.LookAndFeel == LibraryConfig.LookAndFeels.Viewer;
        Reader = !Viewer;
        UseIVPs = config.UseIVPs;
        AnimateIVPs = config.AnimateIVPs;
        ShowPagingInfo = config.ShowPagingInfo;
        ShowAlbumPath = config.ShowAlbumPath;
        ShowPageName = config.ShowPageName;
        ShowImageSize = config.ShowImageSize;
        _libraryConfig = config;
    }

    [RelayCommand]
    public void OkCommand()
    {
        _libraryConfig.Recursive = Recursive;
        _libraryConfig.LookAndFeel = Viewer? LibraryConfig.LookAndFeels.Viewer : LibraryConfig.LookAndFeels.Reader;
        _libraryConfig.UseIVPs = UseIVPs;
        _libraryConfig.AnimateIVPs = AnimateIVPs;
        _libraryConfig.ShowPagingInfo = ShowPagingInfo;
        _libraryConfig.ShowAlbumPath = ShowAlbumPath;
        _libraryConfig.ShowPageName = ShowPageName;
        _libraryConfig.ShowImageSize = ShowImageSize;
        
        // Close the window with a result
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
        {
            var window = app.Windows.FirstOrDefault(w => w.DataContext == this);
            window?.Close(_libraryConfig); // Return the result from ShowDialog
        }
    }
}