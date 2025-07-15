using System;
using System.Collections.Generic;
using Bubbles4.ViewModels;
using Bubbles4.Views;

namespace Bubbles4.Services;
public class ShutdownCoordinator()
{
    public MainWindow? Window { get; set; }

    private readonly HashSet<object> _blockers = new();
    public bool IsShutdownBlocked => !IsShuttingDown || _blockers.Count > 0;
    public bool IsShuttingDown { get; set; }
    public void RegisterBlocker(object key)
    {
        if(!IsShuttingDown)
            throw new InvalidOperationException("Can't register blocker if not shutting down");
        _blockers.Add(key);
    }

    public void UnregisterBlocker(object key)
    {
        if (!IsShuttingDown || _blockers.Count == 0) return;
        if (_blockers.Remove(key) && _blockers.Count == 0)
        {
            TryCloseWindow();
        }
    }

    public void TryCloseWindow()
    {
        if (IsShuttingDown && !IsShutdownBlocked)
            Window?.Close();
    }
}
