using System;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using Bubbles4.Models;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Bubbles4.Services;
using CommunityToolkit.Mvvm.Input;
namespace Bubbles4.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty] private string _greeting = "Welcome to Avalonia!";
    public LibraryViewModel Library { get; set; } = new();
    private string? _configPath;
    private LibraryConfig? _config;
    public LibraryConfig Config
    {
        get
        {
            return (_config == null)? DefaultConfig : _config;
        }
        private set =>_config = value;
    }

    
    public LibraryConfig DefaultConfig { get; private set; }
    string? _libraryPath;
    public string? LibraryPath { get=>_libraryPath; set=>SetProperty(ref _libraryPath, value); }
    
    
    private bool _isFullscreen;
    
    public bool IsFullscreen
    {
        get => _isFullscreen;
        set => SetProperty(ref _isFullscreen, value);
    }

    public ICommand ToggleFullscreenCommand { get; }
    private readonly IDialogService _dialogService;
    
    public MainViewModel(string? arg, IDialogService dialogService)
    {
        _dialogService = dialogService;
        
        
        DefaultConfig = new LibraryConfig();
        ToggleFullscreenCommand = new RelayCommand(() => IsFullscreen = !IsFullscreen);
        if (arg != null && Directory.Exists(arg))
        {
            
            LibraryPath = arg;
            _configPath = arg;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _configPath += "\\.bblconfig";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _configPath += "/.bblconfig";
            }            
            
            
            if (File.Exists(_configPath))
            {
                string configJson = File.ReadAllText(_configPath);
                if(!string.IsNullOrEmpty(configJson) ) _config = LibraryConfig.Deserialize(configJson);
            }
        }
    }
    
    public async Task InitializeAsync()
    {
        if (LibraryPath != null)
        {
            await StartParsingLibraryAsync(LibraryPath);
        }
    }
    
    [RelayCommand]
    private async Task PickDirectoryAsync()
    {
        var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (window != null)
        {
            var selectedPath = await _dialogService.PickDirectoryAsync(window);
            if (!string.IsNullOrEmpty(selectedPath) && Directory.Exists(selectedPath))
            {
                LibraryPath = selectedPath;
                await StartParsingLibraryAsync(selectedPath);
            }
        }
    }
    
    

    private CancellationTokenSource? _parsingCts;

    async Task StartParsingLibraryAsync(string path)
    {
        // Cancel previous parsing run if active
        _parsingCts?.Cancel();
        _parsingCts = new CancellationTokenSource();
        var token = _parsingCts.Token;

        Library.Clear();

        try
        {
            await LibraryParserService.ParseLibraryAsync(path, batch =>
            {
                // Marshal to UI thread
                Dispatcher.UIThread.Post(() =>
                {
                    Library.AddBatch(batch);
                });
            }, cancellationToken: token);
        }
        catch (OperationCanceledException)
        {
            // Optional: handle cancellation gracefully
        }
    }

    public void CancelParsing()
    {
        _parsingCts?.Cancel();
    }
}