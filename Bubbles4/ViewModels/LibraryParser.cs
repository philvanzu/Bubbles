using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Bubbles4.Models;

namespace Bubbles4.ViewModels;

public partial class LibraryViewModel
{
    private ConcurrentDictionary<string, List<BookBase>?> _parsingData = new ();
    private CancellationTokenSource? _parsingCts;

    public virtual async Task StartParsingLibraryAsync(string path, IProgress<(string, double, bool)> progress)
    {
        // Cancel previous parsing run if active
        _parsingCts?.Cancel();
        _parsingCts?.Dispose();
        _parsingCts = new CancellationTokenSource();
        var token = _parsingCts.Token;

        //Clear();

        try
        {
            Dispatcher.UIThread.Post(()=>IsLoading = true);
            var info  = new DirectoryInfo(path);
            var root = new LibraryNodeViewModel(this,info.FullName, info.Name, info.CreationTime, info.LastWriteTime);
            await ParseLibraryNodeAsync(root, token,  book =>
            {
                if(_parsingData[path] == null)
                    _parsingData[path] = new List<BookBase>();
                
                _parsingData[path]!.Add(book);
                // Marshal to UI thread
            }, progress: progress);
            Dispatcher.UIThread.Post(()=>
            {
                List<BookViewModel> batch = new();
                foreach (var node in _parsingData)
                {
                    if (node.Value == null) continue;
                    batch.AddRange(node.Value.Select(bookbase => 
                        new BookViewModel(bookbase, this, node.Key)));
                }
                
                LibraryNodeViewModel? selected = null;
                if (SelectedNode != null)
                    selected = root.FindNode(SelectedNode.Path);
                
                RootNode = root;    
                if(selected != null) SelectedNode = selected;
                
                FinalizeBookCollection(batch);
                OnPropertyChanged(nameof(RootNode));
                IsLoading = false;
            });
        }
        catch (OperationCanceledException)
        {
            // Optional: handle cancellation gracefully
        }

    }

    public void CancelParsing()
    {
        _parsingCts?.Cancel();
        _parsingCts?.Dispose();
        _parsingCts = null;
    }

    public async Task<bool> ParseLibraryNodeAsync(  LibraryNodeViewModel node,
                                                    CancellationToken cancellationToken,
                                                    Action<BookBase>? bookToParent = null,
                                                    int maxParallelism = 4,
                                                    IProgress<(string, double, bool)>? progress = null)
    {
        if (!Directory.Exists(node.Path)) return false;
        
        progress?.Report(($"Loaded Directories : {++node.Root.progressCounter}", -1.0, false));
        var dirInfo = new DirectoryInfo(node.Path);
        var subDirs = dirInfo.GetDirectories();
        var files = dirInfo.GetFiles();

        var bookList = new List<BookBase>();
        var imageCount = 0;

        // Recursively parse subdirectories
        var subTasks = new List<Task<bool>>();
        var childNodes = new List<LibraryNodeViewModel>();

        using var throttler = new SemaphoreSlim(maxParallelism);

        foreach (var subDir in subDirs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var subNode = new LibraryNodeViewModel( this, subDir.FullName, subDir.Name, subDir.CreationTime, subDir.LastWriteTime, node);
            childNodes.Add(subNode);

            await throttler.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var task = Task.Run(async () =>
                {
                    try
                    {
                        return await ParseLibraryNodeAsync(subNode, cancellationToken,
                            (BookBase) => { bookList.Add(BookBase); }, maxParallelism, progress);
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex){Console.WriteLine(ex);}
                    return false;

                }, cancellationToken);
                subTasks.Add(task);
            }
            finally
            {
                throttler.Release();
            }

            
        }

        var subResults = await Task.WhenAll(subTasks);

        // Add only non-empty child nodes
        for (int i = 0; i < subResults.Length; i++)
        {
            if (subResults[i])
            {
                var child = childNodes[i];
                await Dispatcher.UIThread.InvokeAsync(() => node.AddChild(child));
            }
        }
        DateTime lastImageWritten = DateTime.MinValue;
        DateTime firstImageCreated = DateTime.MaxValue;
        // Analyze files
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            BookBase? book = null;

            if (FileAssessor.IsImage(file.Extension))
            {
                if(file.CreationTime < firstImageCreated) 
                    firstImageCreated = file.CreationTime;
                if(file.LastWriteTime > lastImageWritten)
                    lastImageWritten = file.LastWriteTime;
                imageCount++;
                continue;
            }
            else if (FileAssessor.IsArchive(file.Extension))
            {
                if (file.DirectoryName != null)
                    book = new BookArchive(file.FullName, file.Name, -1, file.LastWriteTime, file.CreationTime);
            }
            else if (FileAssessor.IsPdf(file.Extension))
            {
                if (file.DirectoryName != null)
                    book = new BookPdf(file.FullName, file.Name, -1, file.LastWriteTime, file.CreationTime);
            }

            if (book != null)
                bookList.Add(book);
        }

        // Treat this folder as a book if it contains images
        if (imageCount > 0)
        {
            var dirBook = new BookDirectory(dirInfo.FullName, dirInfo.Name, imageCount, lastImageWritten, firstImageCreated);
            if(node.Parent == null) bookList.Add(dirBook);
            else if(bookToParent != null) 
                await Dispatcher.UIThread.InvokeAsync(() => { bookToParent(dirBook); });
        }

        // Add books to this node
        if (bookList.Count > 0)
            AddBatch(node.Path, bookList);
        
        node.IsLoaded = true;

        // Return true if this node or any of its children has books
        return bookList.Count > 0 || subResults.Any(x => x);
    }
    
    private void AddBatch(string nodeId, List<BookBase> batch)
    {
        _parsingData[nodeId] = batch;
    }
    
    public string Serialize()
    {
        Dictionary<string, List<string>> books = new();
        foreach (var item in _books)
        {
            if(!books.ContainsKey(item.LibraryNodeId))
                books[item.LibraryNodeId] = new List<string>();
            
            var serializedModel = item.Model.Serialize();
            books[item.LibraryNodeId].Add( serializedModel);
        }
        LibraryData data = new()
        {
            Books = JsonSerializer.Serialize(books),
            Nodes = RootNode.Serialize(),
        };
        return JsonSerializer.Serialize(data);
    }
    
    public async Task<bool> LoadSerializedCollection(string json, IProgress<(string, double, bool)> progress)
    {
        Dispatcher.UIThread.Post(()=>IsLoading = true);
        var data = JsonSerializer.Deserialize<LibraryData>(json);
        if (data != null)
        {
            var node = LibraryNodeViewModel.Load(data.Nodes, this);
            if (node != null)
            {
                RootNode = node;

                var books = JsonSerializer.Deserialize<Dictionary<string, List<String>>>(data.Books);
                if (books != null)
                {
                    List<BookViewModel> bvms = new();
                    var total = books.Count;
                    int i = 0;
        
                    foreach (var kv in books)
                    {
                        var nodeId = kv.Key;
                        foreach (var bbjson in kv.Value)
                        {
                            var bookbase = BookBase.Deserialize(bbjson);
                            if (bookbase != null)
                                bvms.Add(new BookViewModel(bookbase, this, nodeId));    
                        }
                        progress.Report(($"Loading Cached Library Data...", (double)++i/total, false));
                    }    
                    progress.Report(($"Loading Cached Library Data...", -1, false));
        
                    await Dispatcher.UIThread.InvokeAsync(()=>
                    {
                        FinalizeBookCollection(bvms, false);
                        RootNode.SortChildren(Config.NodeSortOption, Config.NodeSortAscending);
                    });        
                    progress.Report(($"Loading Cached Library Data...", 0, true));
                    return true;    
                }
            }
        }
        
        return false;
    }


    public virtual void FinalizeBookCollection(List<BookViewModel> batch, bool authoritative = true)
    {
        if (!Dispatcher.UIThread.CheckAccess())
            throw new InvalidOperationException("LibraryViewModel.AddBatch must be invoked on the UI thread.");

        if (!authoritative && _books.Count > 0)
            return;

        if (authoritative && _books.Count > 0)
        {
            var vmLookup = _books.ToDictionary(vm => vm.Path);
            var incomingPaths = new HashSet<string>(batch.Select(b => b.Path));
            int removed = 0, added = 0;
            // Remove any existing items not in the incoming batch
            for (int i = _books.Count - 1; i >= 0; i--)
            {
                if (!incomingPaths.Contains(_books[i].Path))
                {
                    _books.RemoveAt(i);
                    removed++;
                }
                    
            }
    
            // Add new ones not already in the VM list
            foreach (var newBook in batch)
            {
                if (!vmLookup.ContainsKey(newBook.Path))
                {
                    _books.Add(newBook);
                    added++;
                }
            }
            Console.WriteLine($"Authoritative set included. removes: {removed}, added: {added}");
            if (added > 0 || removed > 0)
            {
                Sort();
                MainViewModel.UpdateLibraryStatus();
                OnPropertyChanged(nameof(Count));        
            }
        }
        else
        {
            _books.Clear();
            _books.AddRange(batch);
            Sort();
            MainViewModel.UpdateLibraryStatus();
            OnPropertyChanged(nameof(Count));
        }
    }

    //when file system watch system requires adding a new branch to the node tree
    //Warning, if starting to use it from somewhere else, will need to add guards against many conditions
    //currently assumes directoryPath contains parent.Path and they are not the same, and both exist.
    //Does not check if any new node contains books
    public LibraryNodeViewModel? AddNode(LibraryNodeViewModel parent, string directoryPath)
    {
        var separator = System.IO.Path.DirectorySeparatorChar;
        string relativePath = directoryPath.Length == parent.Path.Length
            ? string.Empty
            : directoryPath.Substring(parent.Path.Length).TrimStart(separator);
        
        var segments = relativePath.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        var currentNode = parent;

        int depth = 0;
        
        while (depth < segments.Length)
        {
            var segment = segments[depth];
            var currentPath = currentNode.Path;
            var nextinfo = new DirectoryInfo(System.IO.Path.Combine(currentPath, segment));
            if (!nextinfo.Exists) break;
            var nextNode = new LibraryNodeViewModel(this, nextinfo.FullName, nextinfo.Name, 
                nextinfo.CreationTime, nextinfo.LastWriteTime, currentNode);
            
            currentNode.AddChild(nextNode);
            currentNode = nextNode;
            depth++;
        }
        parent.SortChildren();
        return currentNode;
    }

    private void RemoveNodeIfEmpty(LibraryNodeViewModel node)
    {
        
        LibraryNodeViewModel? removeNode = null;
        
        do
        {
            if (NodeBooksCount(node.Path) > 0) break;
            removeNode = node;

            if (node.Parent == null) break;
            node = node.Parent;
            if (node.Children.Count > 1) break;
        } 
        while(true);

        if (removeNode != null)
        {
            node.RemoveChild(removeNode);
            node.SortChildren();
        }
    }

    class LibraryData
    {
        public required string Books { get; set; }
        public required string Nodes { get; set; }
    }
}



