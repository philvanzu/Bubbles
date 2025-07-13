using System;
using Avalonia.Controls;
using Avalonia;
using System.Runtime.InteropServices;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Bubbles4.Controls;
using Bubbles4.Models;
using Bubbles4.Services;
using Bubbles4.ViewModels;

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
    private float _hideCursorAfterElapsed = 5f;
    private bool _cursorVisible = true;
    private readonly DispatcherTimer _cursorTimer;
    public FastImageViewer? ImgViewer { get; set; }
    
    
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

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        
        if(AppStorage.Instance.UserSettings.HideCursorTime > 0)
            _hideCursorAfterElapsed = AppStorage.Instance.UserSettings.HideCursorTime;
        
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
            //Console.WriteLine("window, exiting fullscreen)");
            if(ImgViewer != null)ImgViewer.OnExitingFullscreen();
            ExitFullscreen();
            //Console.WriteLine("window, exited fullscreen)");
        }
        else
        {
            //Console.WriteLine("window, entering fullscreen)");
            EnterFullscreen();
            //Console.WriteLine("window, entered fullscreen)");
            //if(ImgViewer != null)ImgViewer.OnEnteringFullscreen();
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
            // Remove window borders and title bar
            var style = Win32.GetWindowLong(hwnd, Win32.GWL_STYLE);
            style &= ~(Win32.WS_CAPTION | Win32.WS_THICKFRAME | Win32.WS_OVERLAPPEDWINDOW);
            Win32.SetWindowLong(hwnd, Win32.GWL_STYLE, style);

            var exStyle = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
            exStyle &= ~(Win32.WS_EX_DLGMODALFRAME | Win32.WS_EX_WINDOWEDGE | Win32.WS_EX_CLIENTEDGE | Win32.WS_EX_STATICEDGE);
            Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, exStyle);

            if (Screens.Primary != null)
            {
                var width = Screens.Primary.Bounds.Width;
                var height = Screens.Primary.Bounds.Height;

                // Move window to (0,0) and size it to full screen
                Win32.SetWindowPos(hwnd, Win32.HWND_TOP,
                    0, 0, width, height,
                    Win32.SWP_NOZORDER | Win32.SWP_FRAMECHANGED | Win32.SWP_SHOWWINDOW);
            }
        }
        else
        {
            // Restore window styles
            var style = Win32.GetWindowLong(hwnd, Win32.GWL_STYLE);
            style |= Win32.WS_OVERLAPPEDWINDOW;
            Win32.SetWindowLong(hwnd, Win32.GWL_STYLE, style);

            Win32.SetWindowPos(hwnd, IntPtr.Zero, 100, 100, 800, 600,
                Win32.SWP_NOZORDER | Win32.SWP_FRAMECHANGED | Win32.SWP_SHOWWINDOW);
        }
    }

    

    

}