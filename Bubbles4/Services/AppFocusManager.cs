namespace Bubbles4.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

public class AppFocusManager
{
    public static AppFocusManager Instance { get; } = new AppFocusManager();

    private readonly HashSet<Window> _trackedWindows = new();
    private readonly HashSet<Window> _activeWindows = new();

    private bool _isAppActive;

    public event EventHandler? AppGainedFocus;
    public event EventHandler? AppLostFocus;

    private AppFocusManager() { }

    public void RegisterWindow(Window window)
    {
        if (_trackedWindows.Contains(window))
            return;

        _trackedWindows.Add(window);
        if (window.IsActive)
            _activeWindows.Add(window);
        window.Activated += OnWindowActivated;
        window.Deactivated += OnWindowDeactivated;
        window.Closed += OnWindowClosed;
        Console.WriteLine($"Window registered: {window.Title}");
        UpdateFocusState();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (sender is Window w)
            UnregisterWindow(w);
    }

    public void UnregisterWindow(Window window)
    {
        if (!_trackedWindows.Remove(window))
            return;
        
        _activeWindows.Remove(window);

        window.Activated -= OnWindowActivated;
        window.Deactivated -= OnWindowDeactivated;
        window.Closed -= OnWindowClosed;

        UpdateFocusState();
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        if (sender is Window w)
        {
            _activeWindows.Add(w);
            UpdateFocusState();
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (sender is Window w)
        {
            _activeWindows.Remove(w);
            UpdateFocusState();
        }
    }

    private void UpdateFocusState()
    {
        bool anyActive = _activeWindows.Count > 0;
        if (anyActive != _isAppActive)
        {
            _isAppActive = anyActive;
            if (anyActive)
            {
                Console.WriteLine("Bubbles gained focus");
                AppGainedFocus?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Console.WriteLine("Bubbles lost focus");
                AppLostFocus?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
