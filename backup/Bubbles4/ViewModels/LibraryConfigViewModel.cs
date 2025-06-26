using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Bubbles4.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bubbles4.ViewModels;

public partial class LibraryConfigViewModel : ObservableObject
{
    private LibraryConfig _libraryConfig;
    
    public LibraryConfig.FitTypes[] FitTypes => Enum.GetValues<LibraryConfig.FitTypes>();
    public LibraryConfig.ScrollActions[] ScrollActions => Enum.GetValues<LibraryConfig.ScrollActions>();
    
    [ObservableProperty]bool _includeSubdirectories;
    [ObservableProperty]LibraryConfig.FitTypes _fit;
    [ObservableProperty]LibraryConfig.ScrollActions _scrollAction;
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
        IncludeSubdirectories = config.IncludeSubdirectories;

        Fit = config.Fit;
        ScrollAction = config.ScrollAction;
        UseIVPs = config.UseIVPs;
        AnimateIVPs = config.AnimateIVPs;
        ShowPagingInfo = config.ShowPagingInfo;
        ShowPagingInfoFontSize = config.ShowPagingInfoFontSize;
        ShowAlbumPath = config.ShowAlbumPath;
        ShowAlbumPathFontSize = config.ShowAlbumPathFontSize;
        ShowPageName = config.ShowPageName;
        ShowPageNameFontSize = config.ShowPageNameFontSize;
        ShowImageSize = config.ShowImageSize;
        ShowImageSizeFontSize = config.ShowImageSizeFontSize;
        _libraryConfig = config;
    }

    [RelayCommand]
    public void OkCommand()
    {
        _libraryConfig.IncludeSubdirectories = IncludeSubdirectories;
        _libraryConfig.Fit = Fit;
        _libraryConfig.ScrollAction = ScrollAction;
        _libraryConfig.UseIVPs = UseIVPs;
        _libraryConfig.AnimateIVPs = AnimateIVPs;
        _libraryConfig.ShowPagingInfo = ShowPagingInfo;
        _libraryConfig.ShowPagingInfoFontSize = ShowPagingInfoFontSize;
        _libraryConfig.ShowAlbumPath = ShowAlbumPath;
        _libraryConfig.ShowAlbumPathFontSize = ShowAlbumPathFontSize;
        _libraryConfig.ShowPageName = ShowPageName;
        _libraryConfig.ShowPageNameFontSize = ShowPageNameFontSize;
        _libraryConfig.ShowImageSize = ShowImageSize;
        _libraryConfig.ShowImageSizeFontSize = ShowImageSizeFontSize;
        
        // Close the window with a result
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
        {
            var window = app.Windows.FirstOrDefault(w => w.DataContext == this);
            (window as Window)?.Close(_libraryConfig); // Return the result from ShowDialog
        }
    }
}