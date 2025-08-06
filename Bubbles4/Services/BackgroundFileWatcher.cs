using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Dialogs.Internal;
using Bubbles4.Models;

namespace Bubbles4.Services;

public class BackgroundFileWatcher
{
    private FileSystemWatcher? _watcher;
    private Channel<FileSystemEventArgs> _eventChannel = Channel.CreateUnbounded<FileSystemEventArgs>();
    private CancellationTokenSource? _cts;
    private Task? _processingTask;
    private string? _rootPath;
    private Action<FileSystemEventArgs>? _onFileChanged;

    private readonly ConcurrentDictionary<string, CancellationTokenSource> _debounceTokens = new();
    private readonly TimeSpan _debounceDelay = TimeSpan.FromSeconds(2);

    ~BackgroundFileWatcher()
    {
        StopWatching();
    }
    
    public void StartWatching(string path, bool recursive,
        Action<FileSystemEventArgs> onChanged)
    {
        StopWatching();

        if (!Directory.Exists(path)) return;

        _onFileChanged = onChanged;

        _eventChannel = Channel.CreateUnbounded<FileSystemEventArgs>();
        _cts = new CancellationTokenSource();

        _processingTask = Task.Run(() => ProcessEventsAsync(_cts.Token));
        _rootPath = path;
        _watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = recursive,
            EnableRaisingEvents = true,
            NotifyFilter =  NotifyFilters.FileName |
                            NotifyFilters.DirectoryName |
                            NotifyFilters.LastWrite |    // modification (contents changed)
                            NotifyFilters.CreationTime 
        };

        _watcher.Created += HandleChange;
        _watcher.Deleted += HandleChange;
        _watcher.Changed += HandleChange;
        _watcher.Renamed += HandleRename;
    }

    private void HandleChange(object sender, FileSystemEventArgs e)
    {
        HandleChange(e, false);
    }
    private void HandleChange(FileSystemEventArgs e, bool skipWatchableCheck)
    {
        if (skipWatchableCheck || FileAssessor.IsWatchable(e.FullPath))
        {
            if (e.ChangeType != WatcherChangeTypes.Changed)
            {
                if(!_eventChannel.Writer.TryWrite(e))
                    Console.WriteLine($"Failed to watch event {e.ChangeType} | {e.FullPath}");
            }
        }
    }

    private void HandleRename(object sender, RenamedEventArgs e)
    {
        bool new_ = FileAssessor.IsWatchable(e.FullPath);
        bool old_ = FileAssessor.IsWatchable(e.OldFullPath);
        if (!new_ && !old_) return;

        if (old_ || new_ )
        {
            if(FileAssessor.IsDescendantPath(_rootPath!, e.OldFullPath))
                HandleChange(MakeArgs(WatcherChangeTypes.Deleted, e.OldFullPath), true);
            if(FileAssessor.IsDescendantPath(_rootPath!, e.FullPath))
                HandleChange(MakeArgs(WatcherChangeTypes.Created, e.FullPath), true);
        }
    }
    private static FileSystemEventArgs MakeArgs(WatcherChangeTypes type, string fullPath)
    {
        return new FileSystemEventArgs(type, Path.GetDirectoryName(fullPath)!, Path.GetFileName(fullPath));
    }
    private async Task ProcessEventsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var eventTask = _eventChannel.Reader.WaitToReadAsync(token).AsTask();
                var completed = await Task.WhenAny(eventTask);

                if (await eventTask)
                {
                    while (_eventChannel.Reader.TryRead(out var e))
                    {
                        _onFileChanged?.Invoke(e);
                    }
                }
                // If buffering, just hold the data in the channels
            }
            catch (OperationCanceledException){}
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                break;
            }
        }
    }


    public void StopWatching()
    {
        _cts?.Cancel();
        _processingTask?.Wait();

        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= HandleChange;
            _watcher.Deleted -= HandleChange;
            _watcher.Changed -= HandleChange;
            _watcher.Renamed -= HandleRename;
            _watcher.Dispose();
            _watcher = null;
        }

        _rootPath = null;
        _cts?.Dispose();
        _cts = null;
        _processingTask = null;
    }
}
