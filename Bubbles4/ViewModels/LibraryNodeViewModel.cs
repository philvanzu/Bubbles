using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Threading;
using Bubbles4.Models;
using Bubbles4.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bubbles4.ViewModels;

public partial class LibraryNodeViewModel : LibraryViewModel
{
    private string _name;

    public string Name
    {
        get
        {
            string s = _name;
            if (Books.Count > 0)
            {
                s += $" ({Books.Count})";
            }
            return s;
        } 
    }
    public MainViewModel MainVM => _mainViewModel;
    public LibraryNodeViewModel? Root { get; private set; } = null;
    public LibraryNodeViewModel? Parent { get; private set; } = null;

    [ObservableProperty] private ObservableCollection<LibraryNodeViewModel> _children = new();
    public bool HasChildren => Children != null && Children.Count > 0;

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
    

    
    public LibraryNodeViewModel(MainViewModel mainViewModel, string path, string name, LibraryNodeViewModel? root=null, LibraryNodeViewModel? parent = null)
    : base(mainViewModel, path)
    {
        if(root == null && parent != null)
            throw new ArgumentNullException("Parent provided without a matching Root argument");
        
        Root = parent == null ? this : root;
        Parent = parent;
        _name = name;
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
            await LibraryParserService.ParseLibraryNodeAsync(this, token );
        }
        catch (OperationCanceledException)
        {
            // Optional: handle cancellation gracefully
        }
    }

    // Simulate loading children from disk or another source
    public void AddChild(LibraryNodeViewModel child)
    {
        Children.Add(child);
        if (!child.HasBooks)
        {
            Console.Write("Childless book made it through");
        }
        MainVM.UpdateTreeView();    
    }

    private int CountBooks()
    {
        int count = 0;
        foreach (var child in Children)
            count += child.CountBooks();
        return count + Books.Count;
    }
    public int BookCount => Root.CountBooks();

    public override void AddBatch(List<BookBase> batch)
    {
        base.AddBatch(batch);
        OnPropertyChanged(nameof(BookCount));
        OnPropertyChanged(nameof(Name));
    }


    public bool HasBooks
    {
        get
        {
            bool hasBooks = false;
            foreach (var child in Children)
                if(child.HasBooks ) hasBooks = true;
            
            if (Books?.Count > 0) hasBooks = true;
            
            return hasBooks;
        }
    }
    
}
