using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bubbles4.ViewModels;
using Bubbles4.Models;

namespace Bubbles4.Services;
public class LibraryParserService
{
    public static Task ParseLibraryAsync(
        string rootPath,
        Action<List<BookBase>> onBatchReady,
        int batchSize = 500,
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
