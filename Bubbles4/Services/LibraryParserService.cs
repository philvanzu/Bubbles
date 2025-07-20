using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Bubbles4.ViewModels;
using Bubbles4.Models;

namespace Bubbles4.Services;
public static class LibraryParserService
{

    /// ///////////////////////////////////////////////////////////////////////////////////////
    #region recursive
    public static async Task ParseLibraryRecursiveAsync(
        string rootPath,
        Action<List<BookBase>> onBatchReady,
        int maxParallelism = 4,
        CancellationToken cancellationToken = default,
        IProgress<(string, double, bool)>? progress = null)
    {
        // Step 1: Pre-scan all directories
        progress?.Report(("Starting Library Parsing", -1.0, false));
        List<DirectoryInfo> allDirs = new();
        Console.WriteLine("Collecting Directories");
        Stopwatch reportTimer = Stopwatch.StartNew();
        TimeSpan reportInterval = TimeSpan.FromMilliseconds(200);
        void MaybeReportProgress(string message, double count, bool completed)
        {
            if (reportTimer.Elapsed >= reportInterval)
            {
                progress?.Report(($"{message} : {count}", -1.0, false));
                reportTimer.Restart();
            }
        }
        void CollectDirectories(DirectoryInfo dir)
        {
            allDirs.Add(dir);
            try
            {
                foreach (var sub in dir.GetDirectories())
                    CollectDirectories(sub);
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }

            var count = allDirs.Count;
            MaybeReportProgress($"Counting directories : {count}", -1.0, false);
        }
        
        CollectDirectories(new DirectoryInfo(rootPath));

        int totalDirs = allDirs.Count;
        //Console.WriteLine($"{totalDirs} directories found");
        int dirsProcessed = 0;

        var folders = new ConcurrentQueue<DirectoryInfo>(allDirs);
        var batch = new List<BookBase>();
        var batchLock = new object();

        var tasks = Enumerable.Range(0, maxParallelism).Select(_ => Task.Run(() =>
        {
            while (!cancellationToken.IsCancellationRequested && folders.TryDequeue(out var dir))
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int imageCount = 0;
                    FileInfo[] files = dir.GetFiles();
                    DateTime firstImageCreated = DateTime.MaxValue;
                    DateTime lastImageWritten = DateTime.MinValue;
                    foreach (var file in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        BookBase? result = null;

                        if (FileTypes.IsImage(file.Extension))
                        {
                            if(file.CreationTime < firstImageCreated) 
                                firstImageCreated = file.CreationTime;
                            if(file.LastWriteTime > lastImageWritten)
                                lastImageWritten = file.LastWriteTime;
                            imageCount++;
                        }
                        else if (FileTypes.IsArchive(file.Extension))
                        {
                            result = new BookArchive(file.FullName, file.Name, -1, file.CreationTime, file.LastWriteTime);
                        }
                        else if (FileTypes.IsPdf(file.Extension))
                        {
                            result = new BookPdf(file.FullName, file.Name, -1, file.CreationTime, file.LastWriteTime);
                        }

                        if (result != null)
                        {
                            lock (batchLock)
                            {
                                batch.Add(result);
                            }
                        }
                    }

                    if (imageCount > 0)
                    {
                        var result = new BookDirectory(dir.FullName, dir.Name, imageCount, lastImageWritten, firstImageCreated);
                        lock (batchLock)
                        {
                            batch.Add(result);
                        }
                    }

                    // Report progress
                    if (progress != null)
                    {
                        int current = Interlocked.Increment(ref dirsProcessed);
                        MaybeReportProgress($"Building Library : {current} / {totalDirs}", (double)current / totalDirs, false);
                    }
                }
                catch (UnauthorizedAccessException x) { Console.WriteLine(x.Message); }
                catch (IOException x) { Console.WriteLine(x.Message); }
            }
        }, cancellationToken)).ToArray();

        await Task.WhenAll(tasks);

        // Send any remaining items

        if (batch.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            await Dispatcher.UIThread.InvokeAsync(() => onBatchReady(batch));
        }
    }

    #endregion
    /// ///////////////////////////////////////////////////////////////////////////////////////
    #region node
   public static async Task<bool> ParseLibraryNodeAsync(
    LibraryNodeViewModel node,
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

            var subNode = new LibraryNodeViewModel(node.MainVM, subDir.FullName, subDir.Name, subDir.CreationTime, subDir.LastWriteTime, node);
            childNodes.Add(subNode);

            await throttler.WaitAsync(cancellationToken);
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

            if (FileTypes.IsImage(file.Extension))
            {
                if(file.CreationTime < firstImageCreated) 
                    firstImageCreated = file.CreationTime;
                if(file.LastWriteTime > lastImageWritten)
                    lastImageWritten = file.LastWriteTime;
                imageCount++;
                continue;
            }
            else if (FileTypes.IsArchive(file.Extension))
            {
                if (file.DirectoryName != null)
                    book = new BookArchive(file.FullName, file.Name, -1, file.CreationTime, file.LastWriteTime);
            }
            else if (FileTypes.IsPdf(file.Extension))
            {
                if (file.DirectoryName != null)
                    book = new BookPdf(file.FullName, file.Name, -1, file.CreationTime, file.LastWriteTime);
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
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                node.AddBatch(bookList);
                
            });
        }
        
        node.IsLoaded = true;

        // Return true if this node or any of its children has books
        return bookList.Count > 0 || subResults.Any(x => x);
    }
    ///////////////////////////////
    public static async Task OnNodeLoadedAsync(LibraryNodeViewModel node)
    {
        await Task.CompletedTask;
    }
    #endregion 
    
}
