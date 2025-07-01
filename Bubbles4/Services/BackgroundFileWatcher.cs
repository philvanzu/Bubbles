using System;
using System.Collections.Concurrent;
using System.IO;

namespace Bubbles4.Services;

public class BackgroundFileWatcher : IDisposable
{
    private FileSystemWatcher? _watcher;
    private readonly ConcurrentQueue<FileSystemEventArgs> _buffer = new();
    private readonly ConcurrentQueue<RenamedEventArgs> _renameBuffer = new();
    private volatile bool _buffering = false;

    private Action<FileSystemEventArgs>? _onFileChanged;
    private Action<RenamedEventArgs>? _onFileRenamed;

    public void StartWatching(string path, bool recursive,
        Action<FileSystemEventArgs> onChanged,
        Action<RenamedEventArgs> onRenamed)
    {
        StopWatching();

        if (!Directory.Exists(path)) return;

        _onFileChanged = onChanged;
        _onFileRenamed = onRenamed;

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
        if (_buffering)
            _buffer.Enqueue(e);
        else
            _onFileChanged?.Invoke(e);
    }

    private void HandleRename(object sender, RenamedEventArgs e)
    {
        if (_buffering)
            _renameBuffer.Enqueue(e);
        else
            _onFileRenamed?.Invoke(e);
    }
    /// <summary>
    /// Starts buffering incoming file events.
    /// </summary>
    public void BeginBuffering()
    {
        _buffering = true;
    }
    /// <summary>
    /// Called once initial library parsing is done.
    /// </summary>
    public void FlushBufferedEvents()
    {
        _buffering = false;

        while (_buffer.TryDequeue(out var e))
        {
            _onFileChanged?.Invoke(e);
        }

        while (_renameBuffer.TryDequeue(out var re))
        {
            _onFileRenamed?.Invoke(re);
        }
    }

    public void StopWatching()
    {
        if (_watcher == null) return;

        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= HandleChange;
        _watcher.Deleted -= HandleChange;
        _watcher.Changed -= HandleChange;
        _watcher.Renamed -= HandleRename;

        _watcher.Dispose();
        _watcher = null;
    }

    public void Dispose()
    {
        StopWatching();
        _buffer.Clear();
        _renameBuffer.Clear();
    }
}

