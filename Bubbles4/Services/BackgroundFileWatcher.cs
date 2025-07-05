using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Bubbles4.Services;

public class BackgroundFileWatcher : IDisposable
{
    private FileSystemWatcher? _watcher;
    private Channel<FileSystemEventArgs> _eventChannel = Channel.CreateUnbounded<FileSystemEventArgs>();
    private Channel<RenamedEventArgs> _renameChannel = Channel.CreateUnbounded<RenamedEventArgs>();
    private CancellationTokenSource? _cts;
    private Task? _processingTask;

    private Action<FileSystemEventArgs>? _onFileChanged;
    private Action<RenamedEventArgs>? _onFileRenamed;

    private volatile bool _buffering = false;

    public void StartWatching(string path, bool recursive,
        Action<FileSystemEventArgs> onChanged,
        Action<RenamedEventArgs> onRenamed)
    {
        StopWatching();

        if (!Directory.Exists(path)) return;

        _onFileChanged = onChanged;
        _onFileRenamed = onRenamed;

        _eventChannel = Channel.CreateUnbounded<FileSystemEventArgs>();
        _renameChannel = Channel.CreateUnbounded<RenamedEventArgs>();
        _cts = new CancellationTokenSource();

        _processingTask = Task.Run(() => ProcessEventsAsync(_cts.Token));

        _watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = recursive,
            EnableRaisingEvents = true
        };

        _watcher.Created += HandleChange;
        _watcher.Deleted += HandleChange;
        _watcher.Changed += HandleChange;
        _watcher.Renamed += HandleRename;
    }

    private void HandleChange(object sender, FileSystemEventArgs e)
    {
        if (!_eventChannel.Writer.TryWrite(e))
        {
            // Optionally log or handle overflow
        }
    }

    private void HandleRename(object sender, RenamedEventArgs e)
    {
        if (!_renameChannel.Writer.TryWrite(e))
        {
            // Optionally log or handle overflow
        }
    }

    private async Task ProcessEventsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var eventTask = _eventChannel.Reader.WaitToReadAsync(token).AsTask();
                var renameTask = _renameChannel.Reader.WaitToReadAsync(token).AsTask();

                var completed = await Task.WhenAny(eventTask, renameTask);
                if (completed == eventTask && await eventTask)
                {
                    while (_eventChannel.Reader.TryRead(out var e))
                    {
                        if (!_buffering)
                            _onFileChanged?.Invoke(e);
                    }
                }

                if (completed == renameTask && await renameTask)
                {
                    while (_renameChannel.Reader.TryRead(out var re))
                    {
                        if (!_buffering)
                            _onFileRenamed?.Invoke(re);
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

        while (_renameChannel.Reader.TryRead(out var re))
            _onFileRenamed?.Invoke(re);
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

    public void Dispose()
    {
        StopWatching();
    }
}
