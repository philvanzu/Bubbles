using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Bubbles4.Models;
using Bubbles4.Services;
using CommunityToolkit.Mvvm.ComponentModel;

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
    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }
    public MainViewModel MainVM => _mainViewModel;
    public LibraryNodeViewModel Root { get; private set; } 
    public LibraryNodeViewModel? Parent { get; private set; }

    [ObservableProperty] private ObservableCollection<LibraryNodeViewModel> _children = new();
    public bool HasChildren => Children.Count > 0;

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
    

    
    public LibraryNodeViewModel(MainViewModel mainViewModel, string path, string name, LibraryNodeViewModel? parent = null)
    : base(mainViewModel, path)
    {
        Root = (parent == null) ? this : parent.Root;
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
        int progressCount = 0;
        var progress = new Progress<int>(_ =>
        {
            progressCount++;
            Console.WriteLine(($"Scanning... ({progressCount} folders)"));
        });
        Clear();

        try
        {
            await LibraryParserService.ParseLibraryNodeAsync(this, token, progress: progress);
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
    public int BookCount => Root != null ? Root.CountBooks():-1;

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
            
            if (Books.Count > 0) hasBooks = true;
            
            return hasBooks;
        }
    }

    public void SortChildren(LibraryConfig.SortOptions sortOptions, bool asc)
    {
        
    }
}
