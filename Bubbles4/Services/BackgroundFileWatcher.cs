using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bubbles4.Models;

namespace Bubbles4.Services;

public class BackgroundFileWatcher
{
    private FileSystemWatcher? _watcher;
    private Channel<FileSystemEventArgs> _eventChannel = Channel.CreateUnbounded<FileSystemEventArgs>();
    private CancellationTokenSource? _cts;
    private Task? _processingTask;

    private Action<FileSystemEventArgs>? _onFileChanged;

    private volatile bool _buffering = false;

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
        if (skipWatchableCheck || FileTypes.IsWatchable(e.FullPath))
        {
            if (!_eventChannel.Writer.TryWrite(e))
            {
                // Optionally log or handle overflow
            }    
        }
    }

    private void HandleRename(object sender, RenamedEventArgs e)
    {
        bool new_ = FileTypes.IsWatchable(e.FullPath);
        bool old_ = FileTypes.IsWatchable(e.OldFullPath);
        if (! new_ && !old_) return;

        if (!old_ && new_)
        {
            FileSystemEventArgs args = new FileSystemEventArgs(WatcherChangeTypes.Created, e.FullPath, e.Name);
            HandleChange(args, true);
            return;
        }

        if (old_ && !new_)
        {
            FileSystemEventArgs args = new FileSystemEventArgs(WatcherChangeTypes.Deleted, e.FullPath, e.Name);
            HandleChange(args, true);
            return;
        }

        if (old_ && new_)
        {
            var deleteArgs = new FileSystemEventArgs(WatcherChangeTypes.Deleted, e.OldFullPath, e.OldName);
            HandleChange(deleteArgs, true);
            var createArgs = new FileSystemEventArgs(WatcherChangeTypes.Created, e.FullPath, e.Name);
            HandleChange(createArgs, true);
        }
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
                        if (!_buffering)
                            _onFileChanged?.Invoke(e);
                    }
                }
                // If buffering, just hold the data in the channels
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void BeginBuffering() => _buffering = true;

    public void FlushBufferedEvents()
    {
        _buffering = false;

        while (_eventChannel.Reader.TryRead(out var e))
            _onFileChanged?.Invoke(e);
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

        _cts?.Dispose();
        _cts = null;
        _processingTask = null;
    }
}
