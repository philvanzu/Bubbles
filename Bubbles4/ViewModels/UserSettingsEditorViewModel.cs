using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Bubbles4.Models;
using Bubbles4.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bubbles4.ViewModels;

public partial class UserSettingsEditorViewModel: ViewModelBase
{
    
    [ObservableProperty]private double _mouseSensitivity;
    [ObservableProperty]private double _controllerStickSensitivity;
    [ObservableProperty]private bool _cacheLibraryData;
    [ObservableProperty]int _showPagingInfo;
    [ObservableProperty]int _showAlbumPath;
    [ObservableProperty]int _showPageName;
    [ObservableProperty]int _showImageSize;
    [ObservableProperty] private float _hideCursorTime;
    [ObservableProperty] private double _ivpAnimSpeed;
    [ObservableProperty] private double _turnpageBouncingTime;
    
    [ObservableProperty]ReadOnlyObservableCollection<InputManager.ActionBindings> _actionBindings;
    
    public IEnumerable<InputManager.MouseButton> MouseButtons 
        => Enum.GetValues(typeof(InputManager.MouseButton)).Cast<InputManager.MouseButton>();
    
    [ObservableProperty] private InputManager.MouseButton _dragPanBtn = InputManager.Instance.DragPanButton;
    partial void OnDragPanBtnChanged(InputManager.MouseButton oldValue, InputManager.MouseButton newValue)
    {
        if (newValue == _dragZoomBtn) _dragZoomBtn = oldValue;
        else if (newValue == _drawZoomRectBtn) _drawZoomRectBtn = oldValue;
        RefreshMouseCombos();
    }
    [ObservableProperty] private InputManager.MouseButton _dragZoomBtn = InputManager.Instance.DragZoomButton;
    partial void OnDragZoomBtnChanged(InputManager.MouseButton oldValue, InputManager.MouseButton newValue)
    {
        if (newValue == _dragPanBtn) _dragPanBtn = oldValue;
        else if (newValue == _drawZoomRectBtn) _drawZoomRectBtn = oldValue;
        RefreshMouseCombos();
    }
    [ObservableProperty] private InputManager.MouseButton _drawZoomRectBtn = InputManager.Instance.DrawZoomRectButton;
    partial void OnDrawZoomRectBtnChanged(InputManager.MouseButton oldValue, InputManager.MouseButton newValue)
    {
        if (newValue == _dragPanBtn) _dragPanBtn = oldValue;
        else if (newValue == _dragZoomBtn) _dragZoomBtn = oldValue;
        RefreshMouseCombos();
    }

    void RefreshMouseCombos()
    {
        OnPropertyChanged(nameof(DragPanBtn));
        OnPropertyChanged(nameof(DragZoomBtn));
        OnPropertyChanged(nameof(DrawZoomRectBtn));
    }
    
    
    public IEnumerable<StickName> StickNames 
        => Enum.GetValues(typeof(StickName)).Cast<StickName>();
    [ObservableProperty] private StickName _stickPan = InputManager.Instance.PanStick;
    partial void OnStickPanChanged(StickName oldValue, StickName newValue)
    {
        _stickZoom = oldValue;
        OnPropertyChanged(nameof(StickZoom));
    }

    [ObservableProperty] private StickName _stickZoom = InputManager.Instance.ZoomStick;
    partial void OnStickZoomChanged(StickName oldValue, StickName newValue)
    {
        _stickPan = oldValue;
        OnPropertyChanged(nameof(StickPan));
    }
    IDialogService _dialogService;
    Window _window;
    public UserSettingsEditorViewModel(IDialogService dlg)
    {
        _dialogService = dlg;
        var pref = AppStorage.Instance.UserSettings;
        MouseSensitivity = pref.MouseSensitivity;
        ControllerStickSensitivity = pref.ControllerStickSensitivity;
        HideCursorTime = pref.HideCursorTime;
        IvpAnimSpeed = pref.IvpAnimSpeed;
        TurnpageBouncingTime = pref.TurnPageBouncingTime;
        ShowPagingInfo = pref.ShowPagingInfo;
        ShowAlbumPath = pref.ShowAlbumPath;
        ShowImageSize = pref.ShowImageSize;
        ShowPageName = pref.ShowPageName;

        _actionBindings = new ReadOnlyObservableCollection<InputManager.ActionBindings>(InputManager.Instance.Bindings);
    }

    public void Initialize(Window window)
    {
        _window = window;
        InputManager.Instance.UserSettingsWindow = window;
        InputManager.Instance.DialogService = _dialogService;
    }

    [RelayCommand]
    private void ResetControls()
    {
        _dragPanBtn = InputManager.MouseButton.LeftMouseButton;
        _dragZoomBtn = InputManager.MouseButton.MiddleMouseButton;
        _drawZoomRectBtn = InputManager.MouseButton.RightMouseButton;
        _stickPan = StickName.LStick;
        _stickZoom = StickName.RStick;
        OnPropertyChanged(nameof(DragPanBtn));
        OnPropertyChanged(nameof(DragZoomBtn));
        OnPropertyChanged(nameof(DrawZoomRectBtn));
        OnPropertyChanged(nameof(StickPan));
        OnPropertyChanged(nameof(StickZoom));
    }

    [RelayCommand]
    private void ResetBindings()
    {
        InputManager.Instance.InitializeDefaults();
        ActionBindings = new ReadOnlyObservableCollection<InputManager.ActionBindings>(InputManager.Instance.Bindings);
    }
    [RelayCommand] private void OkPressed()
    {
        UserSettings prefs = new UserSettings()
        {
            MouseSensitivity = MouseSensitivity,
            ControllerStickSensitivity = ControllerStickSensitivity,
            
            HideCursorTime = HideCursorTime,
            IvpAnimSpeed = IvpAnimSpeed,
            TurnPageBouncingTime = TurnpageBouncingTime,
            ShowPagingInfo = ShowPagingInfo,
            ShowAlbumPath = ShowAlbumPath,
            ShowPageName = ShowPageName,
            ShowImageSize = ShowImageSize,
        };  
        
        InputManager.Instance.DragPanButton = DragPanBtn;
        InputManager.Instance.DragZoomButton = DragZoomBtn;
        InputManager.Instance.DrawZoomRectButton = DrawZoomRectBtn;
        InputManager.Instance.PanStick = StickPan;
        InputManager.Instance.ZoomStick = StickZoom;

        _window?.Close(prefs); // Return the result from ShowDialog
    }
}