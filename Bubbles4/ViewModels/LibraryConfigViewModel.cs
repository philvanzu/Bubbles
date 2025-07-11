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

    
    [ObservableProperty] private bool _recursive = true;
    partial void OnRecursiveChanged(bool value)
    {
        Navtree = !value;
        if (!value) CacheLibraryData = false;
    }

    [ObservableProperty]private bool _navtree;
    partial void OnNavtreeChanged(bool value)
    {
        Recursive = !value;
    }

    [ObservableProperty]private bool _viewer;
    partial void OnViewerChanged(bool value)
    {
        if (value)
        {
            Reader = !value;
            UseIVPs = value;
            AnimateIVPs = value;    
        }
    }

    [ObservableProperty]private bool _reader;
    partial void OnReaderChanged(bool value)
    {
        Viewer = !value;
        UseIVPs = !value;
        AnimateIVPs = !value;    

    }


    [ObservableProperty] private bool _useIVPs;
    [ObservableProperty] private bool _animateIVPs;
    [ObservableProperty] private bool _cacheLibraryData;
    
    
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
    public bool IsCreatingLibrary => _dialogService is not null;
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
        CacheLibraryData = config.CacheLibraryData;
        
        BooksSortOption = config.LibrarySortOption;
        BooksAscending = config.LibrarySortAscending; 
        PagesSortOption = config.BookSortOption;
        PagesAscending = config.BookSortAscending;
        
        _libraryConfig = config;
        

        
        
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
            if (window != null)
            {
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
        _libraryConfig.CacheLibraryData = CacheLibraryData;
        
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