using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Bubbles4.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bubbles4.ViewModels;

public partial class LibraryNodeViewModel : LibraryViewModel
{
    public string Name { get; set; } = "";
    public bool IsAlbum { get; set; }

    public MainViewModel MainVM => _mainViewModel;
    public LibraryNodeViewModel? Root { get; private set; } = null;
    public LibraryNodeViewModel? Parent { get; set; } = null;

    [ObservableProperty] private ObservableCollection<LibraryNodeViewModel> _children = new();

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value) && value && !IsLoaded)
            {
            }
        }
    }

    [ObservableProperty] private bool _isLoaded;
    

    
    public LibraryNodeViewModel(MainViewModel mainViewModel, string path, LibraryNodeViewModel? root = null)
    : base(mainViewModel, path)
    {
        Root = root;
    }

    public override async Task StartParsingLibraryAsync(string path)
    {
        // Cancel previous parsing run if active
        _parsingCts?.Cancel();
        _parsingCts?.Dispose();
        _parsingCts = new CancellationTokenSource();
        var token = _parsingCts.Token;

        Clear();

        try
        {
            await LibraryParserService.ParseLibraryNodeAsync(this, 32, 4, token);
        }
        catch (OperationCanceledException)
        {
            // Optional: handle cancellation gracefully
        }
    }

    // Simulate loading children from disk or another source
    public void AddChild(LibraryNodeViewModel child)
    {
        // Clear dummy children
        Children.Add(child);
    }
    
    

    // Dummy child for lazy expansion
    public static LibraryNodeViewModel Dummy => new(null, "Dummy") { Name = "(Loading...)", IsAlbum = false };
}
