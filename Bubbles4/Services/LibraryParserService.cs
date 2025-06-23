using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Bubbles4.ViewModels;
using Bubbles4.Models;

namespace Bubbles4.Services;
public class LibraryParserService
{

    public static async Task<bool> ParseLibraryNodeAsync(
    LibraryNodeViewModel node,
    int batchSize = 32,
    int maxParallelism = 4,
    CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(node.Path)) return false;

        var dirInfo = new DirectoryInfo(node.Path);
        var subDirs = dirInfo.GetDirectories();
        var files = dirInfo.GetFiles();

        var bookList = new List<BookBase>();
        var imageCount = 0;

        // Analyze files
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            BookBase? book = null;

            if (FileTypes.IsImage(file.Extension))
            {
                imageCount++;
                continue;
            }
            else if (FileTypes.IsArchive(file.Extension))
            {
                if (file.DirectoryName != null)
                    book = new BookArchive(file.FullName, file.DirectoryName, -1, file.CreationTime, file.LastWriteTime);
            }
            else if (FileTypes.IsPdf(file.Extension))
            {
                if (file.DirectoryName != null)
                    book = new BookPdf(file.FullName, file.DirectoryName, -1, file.CreationTime, file.LastWriteTime);
            }

            if (book != null)
                bookList.Add(book);
        }

        // Treat this folder as a book if it contains images
        if (imageCount > 0)
        {
            var dirBook = new BookDirectory(dirInfo.FullName, dirInfo.Name, imageCount, dirInfo.CreationTimeUtc, dirInfo.LastWriteTimeUtc);
            bookList.Add(dirBook);
        }

        // Add books to this node
        if (bookList.Count > 0)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                node.AddBatch(bookList);
            });
        }

        // Recursively parse subdirectories
        var subTasks = new List<Task<bool>>();
        var childNodes = new List<LibraryNodeViewModel>();

        using var throttler = new SemaphoreSlim(maxParallelism);

        foreach (var subDir in subDirs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var subNode = new LibraryNodeViewModel(node.MainVM, subDir.FullName)
            {
                Parent = node,
                Name = subDir.Name
            };

            childNodes.Add(subNode);

            await throttler.WaitAsync(cancellationToken);

            var task = Task.Run(async () =>
            {
                try
                {
                    return await ParseLibraryNodeAsync(subNode, batchSize, maxParallelism, cancellationToken);
                }
                finally
                {
                    throttler.Release();
                }
            }, cancellationToken);

            subTasks.Add(task);
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

        node.IsLoaded = true;

        // Return true if this node or any of its children has books
        return bookList.Count > 0 || subResults.Any(x => x);
    }


    
    
    public static Task ParseLibraryRecursiveAsync(
        string rootPath,
        Action<List<BookBase>> onBatchReady,
        int batchSize = 32,
        int maxParallelism = 4,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var batch = new List<BookBase>(batchSize);
            var folders = new ConcurrentQueue<DirectoryInfo>();
            folders.Enqueue(new DirectoryInfo(rootPath));
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

                        foreach (var file in files)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            BookBase? result = null;

                            if (FileTypes.IsImage(file.Extension)) imageCount++;
                            else if (FileTypes.IsArchive(file.Extension))
                            {
                                if (file.DirectoryName != null)
                                    result = new BookArchive(file.FullName, file.DirectoryName, -1, file.CreationTime,
                                        file.LastWriteTime);
                            }
                            else if (FileTypes.IsPdf(file.Extension))
                                if (file.DirectoryName != null)
                                    result = new BookPdf(file.FullName, file.DirectoryName, -1, file.CreationTime,
                                        file.LastWriteTime);

                            if (result != null)
                            {
                                lock (batchLock)
                                {
                                    batch.Add(result);
                                    if (batch.Count >= batchSize)
                                    {
                                        onBatchReady(new List<BookBase>(batch));
                                        batch.Clear();
                                    }
                                }
                            }
                        }

                        if (imageCount > 0)
                        {
                            var result = new BookDirectory(dir.FullName, dir.Name, imageCount, dir.CreationTimeUtc, dir.LastWriteTimeUtc);
                            lock (batchLock)
                            {
                                batch.Add(result);
                                if (batch.Count >= batchSize)
                                {
                                    onBatchReady(new List<BookBase>(batch));
                                    batch.Clear();
                                }
                            }
                        }

                        foreach (var subDir in dir.GetDirectories())
                        {
                            folders.Enqueue(subDir);
                        }
                    }
                    catch (UnauthorizedAccessException x) { Console.WriteLine(x.Message);}
                    catch (IOException x) { Console.WriteLine(x.Message);}
                }
            }, cancellationToken)).ToArray();

            Task.WaitAll(tasks, cancellationToken);

            // Send remaining items
            lock (batchLock)
            {
                if (batch.Count > 0 && !cancellationToken.IsCancellationRequested)
                {
                    onBatchReady(batch);
                }
            }
        }, cancellationToken);
    }
}
