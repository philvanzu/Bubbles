using System;
using System.IO;

using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using Bubbles4.Models;
using Bubbles4.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bubbles4.ViewModels;

public partial class LibraryConfigViewModel : ObservableObject
{
    private LibraryConfig _libraryConfig;

    [ObservableProperty] private string _windowTitle;
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
    
    [ObservableProperty] private bool _pickDirectory;
    
    private readonly Geometry _upGeometry = Geometry.Parse("M 4 10 L 8 6 L 12 10");
    private readonly Geometry _downGeometry = Geometry.Parse("M 4 6 L 8 10 L 12 6");

    [ObservableProperty] private LibraryConfig.SortOptions _booksSortOption = LibraryConfig.SortOptions.Path;
    [ObservableProperty] private Geometry _booksArrow;
    
    private bool BooksAscending
    {
        get => BooksArrow == _upGeometry;
        set
        {
            BooksArrow = (value) ? _upGeometry : _downGeometry;
            OnPropertyChanged(nameof(BooksArrow));
        }
    }

    [ObservableProperty] private LibraryConfig.SortOptions _pagesSortOption = LibraryConfig.SortOptions.Alpha;
    [ObservableProperty] private Geometry _pagesArrow;
    private bool PagesAscending
    {
        get => PagesArrow == _upGeometry;
        set
        {
            PagesArrow = (value) ? _upGeometry : _downGeometry;
            OnPropertyChanged(nameof(PagesArrow));
        }
    }

    public string[] SortOptions => Enum.GetNames(typeof(LibraryConfig.SortOptions));
    
    IDialogService? _dialogService;    
    public LibraryConfigViewModel(LibraryConfig config, IDialogService? dlgService=null)
    {
        if (dlgService != null)
        {
            PickDirectory = true;
            _dialogService = dlgService;
            WindowTitle = "Configure New Library Options";
        }
        else WindowTitle = "Library Config";
        
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
        
        BooksSortOption = config.LibrarySortOption;
        BooksAscending = config.LibrarySortAscending; 
        PagesSortOption = config.BookSortOption;
        PagesAscending = config.BookSortAscending;
        
        
        PagesArrow = PagesAscending ? _upGeometry : _downGeometry; 
        BooksArrow = BooksAscending ? _upGeometry: _downGeometry;

    }


    [RelayCommand]
    private void ToggleBooksAscending()
    {
        BooksAscending = !BooksAscending;
    }
    [RelayCommand]
    private void TogglePagesAscending()
    {
        PagesAscending = !PagesAscending;
    }
    [RelayCommand]
    private async Task OpenDirectoryPickerAsync()
    {
        if (_dialogService == null) return;
        await  Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            
            string? selectedPath;
            selectedPath = await _dialogService.PickDirectoryAsync(window);

            if (!string.IsNullOrEmpty(selectedPath) && Directory.Exists(selectedPath))
            {
                selectedPath = selectedPath.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                selectedPath += System.IO.Path.DirectorySeparatorChar;
                _libraryConfig.Path = selectedPath;
                OnPropertyChanged(nameof(Path));
                OnPropertyChanged(nameof(CanOk));
            }
        });

    }
    [RelayCommand]
    private void Ok()
    {
        if (!Directory.Exists(_libraryConfig.Path)) return;
        _libraryConfig.Recursive = Recursive;
        _libraryConfig.LookAndFeel = Viewer? LibraryConfig.LookAndFeels.Viewer : LibraryConfig.LookAndFeels.Reader;
        _libraryConfig.UseIVPs = UseIVPs;
        _libraryConfig.AnimateIVPs = AnimateIVPs;
        _libraryConfig.ShowPagingInfo = ShowPagingInfo;
        _libraryConfig.ShowAlbumPath = ShowAlbumPath;
        _libraryConfig.ShowPageName = ShowPageName;
        _libraryConfig.ShowImageSize = ShowImageSize;
        _libraryConfig.LibrarySortOption = BooksSortOption;
        _libraryConfig.LibrarySortAscending = BooksAscending;
        _libraryConfig.BookSortOption = PagesSortOption;
        _libraryConfig.BookSortAscending = PagesAscending;
        
        // Close the window with a result
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
        {
            var window = app.Windows.FirstOrDefault(w => w.DataContext == this);
            window?.Close(_libraryConfig); // Return the result from ShowDialog
        }
    }

    public bool CanOk => Directory.Exists(Path);
}