using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Bubbles4.Models;
using Bubbles4.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using DynamicData.Binding;

namespace Bubbles4.ViewModels;

public partial class LibraryNodeViewModel : LibraryViewModel
{
    private string _name;

    public string Name
    {
        get
        {
            string s = _name;
            if (_books.Count > 0)
            {
                s += $" ({_books.Count})";
            }
            return s;
        } 
    }
    public DateTime Created { get; init; }
    public DateTime Modified { get; init; }
    public MainViewModel MainVM => _mainViewModel;
    public LibraryNodeViewModel Root { get; private set; } 
    public LibraryNodeViewModel? Parent { get; private set; }

    private List<LibraryNodeViewModel> _children = new ();
    private ObservableCollection<LibraryNodeViewModel> _childrenMutable = new();
    public ReadOnlyObservableCollection<LibraryNodeViewModel> Children { get; }
    private LibraryConfig.NodeSortOptions _currentChildrenSort = LibraryConfig.NodeSortOptions.Alpha;
    private bool _currentChildrenSortAscending = true;
    public bool HasChildren => _childrenMutable.Count > 0;

    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isLoaded;
    
    
    public LibraryNodeViewModel? SelectedNode { get; set; }
    
    public LibraryNodeViewModel(MainViewModel mainViewModel, string path, string name, DateTime created, DateTime modified, LibraryNodeViewModel? parent = null)
    : base(mainViewModel, path)
    {
        Root = (parent == null) ? this : parent.Root;
        Parent = parent;
        _name = name;
        Created = created;
        Modified = modified;
        Children = new ReadOnlyObservableCollection<LibraryNodeViewModel>(_childrenMutable);
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
        if (Dispatcher.UIThread.CheckAccess())
        {
            _children.Add(child);
            _childrenMutable.Add(child);
            OnPropertyChanged(nameof(Children));
            MainVM.UpdateTreeView();
        }
        else
        {
            Dispatcher.UIThread.Post(() => AddChild(child));
        }
    }


    private int CountBooks()
    {
        int count = 0;
        foreach (var child in _children)
            count += child.CountBooks();
        return count + _books.Count;
    }
    public int BookCount => Root.CountBooks();

    public override void AddBatch(List<BookBase> batch)
    {
        base.AddBatch(batch);
        
        OnPropertyChanged(nameof(BookCount));
        OnPropertyChanged(nameof(Name));
    }

    public void SortChildren(LibraryConfig.NodeSortOptions sortOption, bool ascending)
    {
        _childrenMutable.Clear();
        var sorted = _children.OrderBy(x => x, GetComparer(sortOption, ascending));
        _childrenMutable.AddRange(sorted);
        OnPropertyChanged(nameof(Children));
        _currentChildrenSort = sortOption;
        _currentChildrenSortAscending = ascending;
        
        foreach (var child in _children)child.SortChildren(sortOption, ascending);
    }
    private IComparer<LibraryNodeViewModel> GetComparer(LibraryConfig.NodeSortOptions sort, bool ascending)
    {
        return sort switch
        {
            LibraryConfig.NodeSortOptions.Alpha => ascending
                ? SortExpressionComparer<LibraryNodeViewModel>.Ascending(x => x.Name)
                : SortExpressionComparer<LibraryNodeViewModel>.Descending(x => x.Name),

            LibraryConfig.NodeSortOptions.Created => ascending
                ? SortExpressionComparer<LibraryNodeViewModel>.Ascending(x => x.Created)
                : SortExpressionComparer<LibraryNodeViewModel>.Descending(x => x.Created),

            LibraryConfig.NodeSortOptions.Modified => ascending
                ? SortExpressionComparer<LibraryNodeViewModel>.Ascending(x => x.Modified)
                : SortExpressionComparer<LibraryNodeViewModel>.Descending(x => x.Modified),

            _ => SortExpressionComparer<LibraryNodeViewModel>.Ascending(x => x.Name)
        };
    }
    public void ReverseChildrenSortOrder()
    {
        SortChildren(_currentChildrenSort, !_currentChildrenSortAscending);
    }

    public bool HasBooks
    {
        get
        {
            bool hasBooks = false;
            foreach (var child in _children)
                if(child.HasBooks ) hasBooks = true;
            
            if (_books.Count > 0) hasBooks = true;
            
            return hasBooks;
            
        }
    }



    #region FileSystemWatcherEvents

    //delegate to selected node and just do a botched job of it
    //too much complexity to care
    public override void FileSystemChanged(FileSystemEventArgs e)
    {
        if (SelectedNode is LibraryViewModel lvm)
            lvm.FileSystemChanged(e);
    }
    public override void FileSystemRenamed(RenamedEventArgs e)
    {
        if (SelectedNode is LibraryViewModel lvm)
            lvm.FileSystemRenamed(e);
    }
    public LibraryNodeViewModel? FindOwnerNode(string path)
    {
        if (!path.StartsWith(Path, StringComparison.OrdinalIgnoreCase))
            return null;

        foreach (var child in _children)
        {
            if (path.StartsWith(child.Path, StringComparison.OrdinalIgnoreCase))
            {
                var match = child.FindOwnerNode(path);
                if (match != null)
                    return match;
            }
        }

        return this;
    }
    

    
    private int _childrenSortRunning = 0;
    private int _childrenSortPending = 0;

    public void EnqueueSortChildrenJob()
    {
        if (Interlocked.CompareExchange(ref _childrenSortRunning, 1, 0) == 0)
        {
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    do
                    {
                        Interlocked.Exchange(ref _childrenSortPending, 0);
                        SortChildren(_currentChildrenSort, _currentChildrenSortAscending);
                        // Optionally await a small delay or yield to UI thread if sorting is expensive
                    }
                    while (Interlocked.CompareExchange(ref _childrenSortPending, 0, 0) != 0);
                }
                finally
                {
                    Interlocked.Exchange(ref _childrenSortRunning, 0);
                }
            });
        }
        else
        {
            // Mark that a sort is pending while one is running
            Interlocked.Exchange(ref _childrenSortPending, 1);
        }
    }
    
    #endregion
}
