using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using Avalonia.Threading;
using Bubbles4.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using DynamicData.Binding;

namespace Bubbles4.ViewModels;

public partial class LibraryNodeViewModel : ViewModelBase
{
    public int progressCounter;
    public LibraryViewModel Library { get; set; }
    [ObservableProperty] private string _path;
    private string _name;

    public string Name
    {
        get
        {
            var count = Library.NodeBooksCount(Path);
            if (count > 0) return $"{_name}({count})";
            return _name;
        }
        init => _name = value;
    }

    public DateTime Created { get; init; }
    public DateTime Modified { get; init; }

    public LibraryNodeViewModel Root { get; private set; }
    public LibraryNodeViewModel? Parent { get; private set; }

    private List<LibraryNodeViewModel> _children = new();
    private ObservableCollection<LibraryNodeViewModel> _childrenMutable = new();
    public ReadOnlyObservableCollection<LibraryNodeViewModel> Children { get; }

    public LibraryConfig.NodeSortOptions CurrentSort { get; set; }
        = LibraryConfig.NodeSortOptions.Alpha;

    public bool CurrentAscending { get; set; } = true;

    public bool HasChildren => _childrenMutable.Count > 0;

    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isLoaded;


    public LibraryNodeViewModel? SelectedNode { get; set; }
    public LibraryNodeViewModel? FindNode( string directoryPath )
    {
        if(string.Equals(directoryPath.TrimEnd(System.IO.Path.DirectorySeparatorChar), 
               Path.TrimEnd(System.IO.Path.DirectorySeparatorChar),
               StringComparison.OrdinalIgnoreCase))
            return this;
        
        foreach (var child in Children)
        {
            var node =  child.FindNode( directoryPath );
            if (node != null) return node;
        }
        return null;
    }

    public LibraryNodeViewModel? FindClosestNode(string fullPath)
    {
        var separator = System.IO.Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(Root.Path, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, Root.Path, StringComparison.OrdinalIgnoreCase))
        {
            return null; // Path doesn't even start under root
        }

        string relativePath = fullPath.Length == Root.Path.Length
            ? string.Empty
            : fullPath.Substring(Root.Path.Length).TrimStart(separator);

        if (string.IsNullOrEmpty(relativePath))
            return Root; // exact match with root

        var segments = relativePath.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        var currentNode = Root;
        int depth = 0;

        while (depth < segments.Length)
        {
            var segment = segments[depth];

            var next = currentNode._children.FirstOrDefault(child =>
            {
                var childName = System.IO.Path.GetFileName(child.Path);
                return string.Equals(childName, segment, StringComparison.OrdinalIgnoreCase);
            });

            if (next == null)
                return currentNode; // No further match — return current as closest

            currentNode = next;
            depth++;
        }

        return currentNode; // Full path matched
    }



    public LibraryNodeViewModel(LibraryViewModel library, string path, string name, DateTime created, DateTime modified,
        LibraryNodeViewModel? parent = null)

    {
        Library = library;
        Root = (parent == null) ? this : parent.Root;
        Parent = parent;
        _path = path;
        _name = name;
        Created = created;
        Modified = modified;
        Children = new ReadOnlyObservableCollection<LibraryNodeViewModel>(_childrenMutable);
    }

    public void AddChild(LibraryNodeViewModel child)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            _children.Add(child);
            _childrenMutable.Add(child);
            OnPropertyChanged(nameof(Children));
        }
        else
        {
            Dispatcher.UIThread.Post(() => AddChild(child));
        }
    }

    public void RemoveChild(LibraryNodeViewModel child)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            _children.Remove(child);
            _childrenMutable.Remove(child);
            OnPropertyChanged(nameof(Children));
        }
        else
        {
            Dispatcher.UIThread.Post(() => RemoveChild(child));
        }
        
    }

    public void BooksCountChanged()
    {
        OnPropertyChanged(nameof(Name));
    }
    public void SetChildren(List<LibraryNodeViewModel> children)
    {
        _children = children;
    }

    public void SortChildren(LibraryConfig.NodeSortOptions? sortOption=null, bool? ascending=null)
    {
        if (sortOption == null) sortOption = CurrentSort;
        if (ascending == null) ascending = CurrentAscending;
        
        _childrenMutable.Clear();
        var sorted = _children.OrderBy(x => x, GetComparer(sortOption.Value, ascending.Value));
        _childrenMutable.AddRange(sorted);
        OnPropertyChanged(nameof(Children));
        CurrentSort = sortOption.Value;
        CurrentAscending = ascending.Value;

        foreach (var child in _children) child.SortChildren(sortOption, ascending);
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
        SortChildren(CurrentSort, !CurrentAscending);
    }

    private NodeData Data => new(Path, _name, Created, Modified, _children.Select(x => x.Data).ToList());

    public string Serialize()
    {
        return JsonSerializer.Serialize(Data);
    }

    public static LibraryNodeViewModel? Load(string json, LibraryViewModel library)
    {
        var data =  JsonSerializer.Deserialize<NodeData>(json);
        if(data != null) return NodeData.Unpack(data, null, library);
        return null;
    }
    
    class NodeData
    {
        public string Path { get; init; }
        public string Name { get; init; }
        public DateTime Created { get; init; }
        public DateTime Modified { get; init; }
        public List<NodeData> Children { get; init; }

        public NodeData(string path, string name, DateTime created, DateTime modified, List<NodeData> children)
        {
            Path = path;
            Name = name;
            Created = created;
            Modified = modified;
            Children = children;
        }

        public static LibraryNodeViewModel Unpack(NodeData data, LibraryNodeViewModel? parent, LibraryViewModel library)
        {
            LibraryNodeViewModel result = new(library, data.Path, data.Name, data.Created, data.Modified, parent);
            List<LibraryNodeViewModel> children = new();
            foreach (var child in data.Children)
                children.Add( NodeData.Unpack(child, result, library));
        
            result._children  = children;
            
            return result;
        }
    }
}


