using System;
using Avalonia.Controls;
using Avalonia;
using System.Runtime.InteropServices;
using Avalonia.Input;
using Avalonia.Threading;
using Bubbles4.Services;

namespace Bubbles4.Views;

public partial class MainWindow : Window
{
    private static Cursor? _invisibleCursor;

    private bool _isFullscreen;
    private WindowState _previousState;
    private SystemDecorations _previousStyle;
    private PixelPoint _previousPosition;
    private Size _previousSize;

    private bool _hovered;
    private DateTime _lastMove;
    private const float _hideCursorAfterElapsed = 5f;
    private bool _cursorVisible = true;
    private readonly DispatcherTimer _cursorTimer;
    public MainWindow()
    {
        InitializeComponent();
        PointerEntered += (_, _) => _hovered = true;
        PointerExited += (_, _) => _hovered = false;
        PointerMoved += (_, _) =>
        {
            _lastMove = DateTime.Now;
            if (!_cursorVisible) ShowCursor();
        };
        
        
        
        _cursorTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250) // Poll every 250ms
        };
        _cursorTimer.Tick += (_, _) =>
        {
            if (_isFullscreen && _cursorVisible )
            {
                bool conditionmet = (DateTime.Now - _lastMove).TotalSeconds >= _hideCursorAfterElapsed;
            
                if (_hovered && conditionmet)
                {
                    HideCursor();
                }    
            }
            
        };
        _cursorTimer.Start();
    }
    
    void HideCursor()
    {
        if (_cursorVisible)
        {
            this.Cursor = GetInvisibleCursor();
            _cursorVisible = false;
        }
    }

    void ShowCursor()
    {
        if (!_cursorVisible)
        {
            this.Cursor = new Cursor(StandardCursorType.Arrow);
            _cursorVisible = true;
        }
    }

private static Cursor GetInvisibleCursor()
    {
        if (_invisibleCursor == null)
        {
            var assembly = typeof(MainWindow).Assembly;
            using var stream = assembly.GetManifestResourceStream("Bubbles4.Assets.transparent-cursor.png")!;
            var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);
            _invisibleCursor = new Cursor(bitmap, new PixelPoint(0, 0));
        }

        return _invisibleCursor;
    }
    public void ToggleFullscreen()
    {
        if (_isFullscreen)
        {
            ExitFullscreen();
        }
        else
        {
            EnterFullscreen();
        }
    }

    public void EnterFullscreen()
    {
        
        _previousState = WindowState;
        _previousStyle = SystemDecorations;
        _previousPosition = Position;
        _previousSize = ClientSize;
        
        WindowState = WindowState.Normal; // Must normalize before fullscreen
        SystemDecorations = SystemDecorations.None;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SetWindowsFullscreen(true);
        }
        else
        {
            WindowState = WindowState.FullScreen;
        }

        _isFullscreen = true;
    }

    public void ExitFullscreen()
    {
        if(_cursorVisible == false) ShowCursor();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SetWindowsFullscreen(false);
        }

        SystemDecorations = _previousStyle;
        WindowState = _previousState;
        ClientSize = _previousSize;
        Position = _previousPosition;
        _isFullscreen = false;
    }

    private void SetWindowsFullscreen(bool enable)
    {
        var handle = TryGetPlatformHandle();
        if (handle is null) return;

        var hwnd = handle.Handle;

        if (enable)
        {
            Win32.SetWindowLong(hwnd, Win32.GWL_STYLE,
                Win32.GetWindowLong(hwnd, Win32.GWL_STYLE) & ~Win32.WS_OVERLAPPEDWINDOW);

            if (Screens.Primary != null)
                Win32.SetWindowPos(hwnd, IntPtr.Zero, 0, 0,
                    Screens.Primary.WorkingArea.Width,
                    Screens.Primary.WorkingArea.Height,
                    Win32.SWP_NOZORDER | Win32.SWP_FRAMECHANGED);
        }
        else
        {
            Win32.SetWindowLong(hwnd, Win32.GWL_STYLE,
                Win32.GetWindowLong(hwnd, Win32.GWL_STYLE) | Win32.WS_OVERLAPPEDWINDOW);

            Win32.SetWindowPos(hwnd, IntPtr.Zero, 100, 100, 800, 600, 
                Win32.SWP_NOZORDER | Win32.SWP_FRAMECHANGED);
        }
    }
    
    

}