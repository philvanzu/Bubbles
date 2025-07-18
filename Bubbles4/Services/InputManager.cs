using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Bubbles4.Controls;
using Bubbles4.Models;
using Bubbles4.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace Bubbles4.Services;

public partial class InputManager: IDisposable
{
    #region Definitions
    class InputBindings
    {
        public int? DragZoomButton { get; set; }
        public int? DragPanButton { get; set; }
        public int? DrawZoomRectButton { get; set; }
        public int? ZoomStick { get; set; }
        public int? PanStick { get; set; }
        public string? Bindings { get; set; }
    }
    public record KeyCombo(Key Key, KeyModifiers Modifiers);
    public enum MouseButton
    {
        LeftMouseButton,
        MiddleMouseButton,
        RightMouseButton
    }
    #endregion
    #region init

    private static readonly Lazy<InputManager> _instance = new(() => new InputManager());
    public static InputManager Instance => _instance.Value;
    
    private readonly SdlInputService _sdlInput=new();
    private readonly CancellationTokenSource _cts = new();
    
    
    public static MainViewModel? MainViewModel { get; set; }
    public static FastImageViewer? ImageViewer { get; set; }

    
    
    public bool IsInitialized { get; private set; }
    

    public MouseButton DragZoomButton { get; set; } = MouseButton.MiddleMouseButton;
    public MouseButton DragPanButton { get; set; } = MouseButton.LeftMouseButton;
    public MouseButton DrawZoomRectButton { get; set; } = MouseButton.RightMouseButton;

    public StickName ZoomStick { get; set; } = StickName.RStick;
    public StickName PanStick { get; set; } = StickName.LStick;


    private Dictionary<string, ActionBindings> _bindings = new()
    {
        ["Next Page"] = new ActionBindings("Next Page", Next),
        ["Previous Page"] = new ActionBindings("Previous Page", Previous),
        ["First Page"] = new ActionBindings("First Page", FirstPage),
        ["Last Page"] = new ActionBindings("Last Page", LastPage),
        ["Next Book"] = new ActionBindings("Next Book", NextBook),
        ["Previous Book"] = new ActionBindings("Previous Book", PreviousBook),
        ["Enter Fullscreen"] = new ActionBindings("Enter Fullscreen", EnterFullscreen),
        ["Exit Fullscreen"] = new ActionBindings("Exit Fullscreen", ExitFullscreen),
        ["Toggle Fullscreen"] = new ActionBindings("Toggle Fullscreen", ToggleFullscreen),
        ["Pan Up"] = new ActionBindings("Pan Up", PanUp),
        ["Pan Down"] = new ActionBindings("Pan Down", PanDown),
        ["Pan Left"] = new ActionBindings("Pan Left", PanLeft),
        ["Pan Right"] = new ActionBindings("Pan Right", PanRight),
        ["Zoom In"] = new ActionBindings("Zoom In", ZoomIn),
        ["Zoom Out"] = new ActionBindings("Zoom Out", ZoomOut),
        ["Fit"] = new ActionBindings("Fit", Fit),
        ["Fit Height"] = new ActionBindings("Fit Height", FitH),
        ["Fit Width"] = new ActionBindings("Fit Width", FitW),
        ["Fit Stock"] = new ActionBindings("Fit Stock", FitStock),
    };

    public ObservableCollection<ActionBindings> Bindings => new ObservableCollection<ActionBindings>(_bindings.Values);

    private readonly Dictionary<KeyCombo, ActionBindings> _keyUpMap = new();
    private readonly Dictionary<ButtonName, ActionBindings> _buttonUpMap = new();
    public static HashSet<KeyCombo> UsedKeyCombos { get; } = new();
    public static HashSet<ButtonName> UsedButtons { get; } = new();
    

    #endregion

    
    public InputManager()
    {
        _sdlInput.Initialize();

        
        _sdlInput.ButtonUp += ControllerButtonUp;
        _sdlInput.ButtonDown += ControllerButtonDown;
        _sdlInput.StickUpdated +=  ControllerStickUpdated;
        Task.Run(async() =>
        {
            try
            {
                await _sdlInput.StartPollingAsync(_cts.Token);
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Controller polling task cancelled");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

            }

        });
    }
    public void Dispose()
    {
        _cts.Cancel();
        _sdlInput.Shutdown();
        _cts.Dispose();
    }
    
    #region State
    void Reset()
    {
        IsInitialized = false;
        UsedButtons.Clear();
        UsedKeyCombos.Clear();
        
        _keyUpMap.Clear();
        _buttonUpMap.Clear();
        foreach (var actionBinding in _bindings.Values)
            actionBinding.Clear();
    }

    public void Initialize()
    {
        LoadBindings(AppStorage.Instance.UserSettings.InputBindings);
        if (!IsInitialized)
        {
            InitializeDefaults();
            SaveBindings();
        }
    }
    public void SaveBindings()
    {
        List<ActionBindingItem> bindings = new();
        
        foreach (var binding in _bindings.Values)
            bindings.AddRange(binding.Inputs);
        
        var savedInputBindings = new InputBindings()
        {
            Bindings = JsonSerializer.Serialize(bindings),
            DragPanButton = (int)DragPanButton,
            DragZoomButton = (int)DragZoomButton,
            DrawZoomRectButton = (int)DrawZoomRectButton,
            ZoomStick = (int)ZoomStick,
            PanStick = (int)PanStick,
        };
        var userSettings = AppStorage.Instance.UserSettings;
        userSettings.InputBindings = JsonSerializer.Serialize(savedInputBindings);
        AppStorage.Instance.UserSettings = userSettings;
    }

    private void LoadBindings(string json)
    {
        Reset();
        try
        {
            var data = JsonSerializer.Deserialize<InputBindings>(json);
            if (data != null)
            {
                if (data.DragPanButton == null
                    || data.DragZoomButton == null
                    || data.DrawZoomRectButton == null
                    || data.ZoomStick == null
                    || data.PanStick == null
                    || data.Bindings == null)
                {
                    throw new InvalidOperationException("Action missing in user key bindings. Fall back to default binding.");
                }
                    
                DragPanButton = (MouseButton) data.DragPanButton.Value;
                DragZoomButton = (MouseButton) data.DragZoomButton.Value;
                DrawZoomRectButton = (MouseButton) data.DrawZoomRectButton.Value;
                ZoomStick = (StickName) data.ZoomStick.Value;
                PanStick = (StickName) data.PanStick.Value;
                var bindings = JsonSerializer.Deserialize<List<ActionBindingItem>>(data.Bindings);
                if (bindings != null)
                {
                    // Rebuild each binding’s Action delegate, and the lookup dictionaries
                    foreach (var binding in bindings)
                    {
                        if (_bindings.TryGetValue(binding.ActionName, out var action))
                        {
                            if (binding.KeyCombo != null) action.Add(binding.KeyCombo!);
                            else if (binding.GamepadButton!= null) action.Add(binding.GamepadButton.Value);
                        }
                        else
                        {
                            throw new InvalidOperationException("Action missing in user key bindings. Fall back to default binding.");
                        }
                    }

                    
                    IsInitialized = true;
                }
            }
            else throw new InvalidOperationException("Action missing in user key bindings. Fall back to default binding.");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Reset();
        }
    }
    public void InitializeDefaults()
    {
        Reset();
        _bindings["Next Page"].Add(new KeyCombo(Key.Space, KeyModifiers.None));
        _bindings["Previous Page"].Add(new KeyCombo(Key.Back, KeyModifiers.None));
        _bindings["Previous Page"].Add(new KeyCombo(Key.Space, KeyModifiers.Alt));

        _bindings["Next Book"].Add(new KeyCombo(Key.PageDown, KeyModifiers.None));
        _bindings["Previous Book"].Add(new KeyCombo(Key.PageUp, KeyModifiers.None));
        _bindings["First Page"].Add(new KeyCombo(Key.Home, KeyModifiers.None));
        _bindings["Last Page"].Add(new KeyCombo(Key.End, KeyModifiers.None));
        _bindings["Exit Fullscreen"].Add(new KeyCombo(Key.Escape, KeyModifiers.None));
        _bindings["Toggle Fullscreen"].Add(new KeyCombo(Key.F11, KeyModifiers.None));
        _bindings["Toggle Fullscreen"].Add(new KeyCombo(Key.Enter, KeyModifiers.Alt));

        _bindings["Fit"].Add(new KeyCombo(Key.F, KeyModifiers.None));
        _bindings["Fit Width"].Add(new KeyCombo(Key.W, KeyModifiers.None));
        _bindings["Fit Height"].Add(new KeyCombo(Key.H, KeyModifiers.None));
        _bindings["Fit Stock"].Add(new KeyCombo(Key.S, KeyModifiers.None));
        _bindings["Zoom In"].Add(new KeyCombo(Key.Add, KeyModifiers.None));
        _bindings["Zoom Out"].Add(new KeyCombo(Key.Subtract, KeyModifiers.None));
        _bindings["Pan Up"].Add(new KeyCombo(Key.Up, KeyModifiers.None));
        _bindings["Pan Down"].Add(new KeyCombo(Key.Down, KeyModifiers.None));
        _bindings["Pan Left"].Add(new KeyCombo(Key.Left, KeyModifiers.None));
        _bindings["Pan Right"].Add(new KeyCombo(Key.Right, KeyModifiers.None));
        _bindings["Enter Fullscreen"].Add(ButtonName.A);
        _bindings["Exit Fullscreen"].Add(ButtonName.B);
        _bindings["Next Page"].Add(ButtonName.LTrigger);
        _bindings["Previous Page"].Add(ButtonName.LB);
        _bindings["Next Book"].Add(ButtonName.RTrigger);
        _bindings["Previous Book"].Add(ButtonName.RB);
        _bindings["Fit Height"].Add(ButtonName.DpadUp);
        _bindings["Fit Stock"].Add(ButtonName.DpadDown);
        _bindings["Fit Width"].Add(ButtonName.DpadLeft);
        _bindings["Fit"].Add(ButtonName.DpadRight);
        _bindings["First Page"].Add(ButtonName.Select);
        _bindings["Last Page"].Add(ButtonName.Start);
        IsInitialized = true;
    }
#endregion

    #region handlers
    public void GlobalKeyUp(object? sender, KeyEventArgs e)
    {
        OnUserSettingsEditorKeyUp(sender, e);
        var combo = new KeyCombo(e.Key, e.KeyModifiers);
        if (_keyUpMap.TryGetValue(combo, out var binding))
        {
            binding.Action.Invoke();
            e.Handled = true;
        }
    }

    public void ButtonUp(object? sender, ButtonEventArgs e)
    {
        if (_buttonUpMap.TryGetValue(e.Button, out var binding))
            binding.Action.Invoke();
    }
    private void ControllerStickUpdated(object? __, StickEventArgs e)
    {
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            
            if (ImageViewer != null)
            {
                if (e.Stick == Instance.PanStick)
                {
                    ImageViewer.OnStickPan(e);
                }
                else if (e.Stick == Instance.ZoomStick)
                {
                    ImageViewer.OnStickZoom(e);
                }

            }
        });
    }
    
    private void ControllerButtonUp(object? sender, ButtonEventArgs e)
    {
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            OnUserSettingsEditorButtonUp(sender, e);
            if(!e.Handled)
                ButtonUp(sender, e);
        });
    }
    
    private void ControllerButtonDown(object? sender, ButtonEventArgs e)
    {

    }
    #endregion
    
    #region Actions
    private static void Previous() => MainViewModel?.PreviousCommand.Execute(null);
    private static void Next() => MainViewModel?.NextCommand.Execute(null);
    private static void EnterFullscreen() => MainViewModel?.EnterFullScreenCommand.Execute(null);
    private static void ExitFullscreen() => MainViewModel?.ExitFullScreenCommand.Execute(null);
    private static void ToggleFullscreen() => MainViewModel?.ToggleFullscreenCommand.Execute(null);
    private static void FirstPage() => MainViewModel?.FirstPageCommand.Execute(null);
    private static void LastPage() => MainViewModel?.LastPageCommand.Execute(null);
    private static void NextBook() => MainViewModel?.NextBookCommand.Execute(null);
    private static void PreviousBook() => MainViewModel?.PreviousBookCommand.Execute(null);

    private static void PanUp() => ImageViewer?.OnUpArrowPressed();
    private static void PanDown() => ImageViewer?.OnDownArrowPressed();
    private static void PanLeft() => ImageViewer?.OnLeftArrowPressed();
    private static void PanRight() => ImageViewer?.OnRightArrowPressed();
    private static void Fit() => ImageViewer?.Fit();
    private static void FitH() => ImageViewer?.FitHeight();
    private static void FitW() => ImageViewer?.FitWidth();
    private static void FitStock() => ImageViewer?.FitStock();
    private static void ZoomIn() => ImageViewer?.Zoom(1);
    private static void ZoomOut() => ImageViewer?.Zoom(-1);
    private static void DoNothing(){}
    #endregion
    


}
