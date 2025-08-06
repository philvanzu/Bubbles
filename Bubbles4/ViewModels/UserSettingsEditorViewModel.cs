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
    public string[] IvpAnimationTypes => Enum.GetNames(typeof(IvpAnimationType));
    
    [ObservableProperty]private double _mouseSensitivity;
    [ObservableProperty]private double _controllerStickSensitivity;
    [ObservableProperty] private double _scrollSpeed;
    [ObservableProperty]private bool _cacheLibraryData;
    [ObservableProperty]int _showPagingInfo;
    [ObservableProperty]int _showAlbumPath;
    [ObservableProperty]int _showPageName;
    [ObservableProperty]int _showImageSize;
    [ObservableProperty] private float _hideCursorTime;
    [ObservableProperty] private double _ivpAnimSpeed;
    [ObservableProperty] IvpAnimationType _ivpAnimType;
    [ObservableProperty] private double _turnpageBouncingTime;
    [ObservableProperty] private int _cropResizeToMax;
    [ObservableProperty] private int _bookmarkValidity;
    [ObservableProperty] private string? _realesrgan_ncnn_vulkan_path; 
    [ObservableProperty]ReadOnlyObservableCollection<InputManager.ActionBindings> _actionBindings;
    
    public IEnumerable<InputManager.MouseButton> MouseButtons 
        => Enum.GetValues(typeof(InputManager.MouseButton)).Cast<InputManager.MouseButton>();
    
    public IEnumerable<ButtonName> ButtonNames
        => Enum.GetValues(typeof(ButtonName)).Cast<ButtonName>();
    
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
    [ObservableProperty] private ButtonName? _stickInverter = InputManager.Instance.StickInverter;
    

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
        _ = newValue;
        _stickZoom = oldValue;
        OnPropertyChanged(nameof(StickZoom));
    }

    [ObservableProperty] private StickName _stickZoom = InputManager.Instance.ZoomStick;
    partial void OnStickZoomChanged(StickName oldValue, StickName newValue)
    {
        _ = newValue;
        _stickPan = oldValue;
        OnPropertyChanged(nameof(StickPan));
    }
    IDialogService _dialogService;
    Window? _window;
    public UserSettingsEditorViewModel(IDialogService dlg)
    {
        _dialogService = dlg;
        var pref = AppStorage.Instance.UserSettings;
        MouseSensitivity = pref.MouseSensitivity;
        ControllerStickSensitivity = pref.ControllerStickSensitivity;
        ScrollSpeed = pref.ScrollSpeed;
        HideCursorTime = pref.HideCursorTime;
        IvpAnimSpeed = pref.IvpAnimSpeed;
        IvpAnimType = pref.IvpAnimationType;
        TurnpageBouncingTime = pref.TurnPageBouncingTime;
        ShowPagingInfo = pref.ShowPagingInfo;
        ShowAlbumPath = pref.ShowAlbumPath;
        ShowImageSize = pref.ShowImageSize;
        ShowPageName = pref.ShowPageName;
        CropResizeToMax = pref.CropResizeToMax;
        BookmarkValidity = pref.BookmarkValidity;
        Realesrgan_ncnn_vulkan_path = pref.Realesrgan_ncnn_vulkan_path;
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
        InputManager.Instance.ResetAxesToDefault();
        _dragPanBtn = InputManager.Instance.DragPanButton;
        _dragZoomBtn = InputManager.Instance.DragZoomButton;
        _drawZoomRectBtn = InputManager.Instance.DrawZoomRectButton;
        _stickPan = InputManager.Instance.PanStick;
        _stickZoom = InputManager.Instance.ZoomStick;
        StickInverter = InputManager.Instance.StickInverter;
        RefreshMouseCombos();
        OnPropertyChanged(nameof(StickPan));
        OnPropertyChanged(nameof(StickZoom));
    }

    [RelayCommand]
    private void ResetBindings()
    {
        InputManager.Instance.ResetActionBindingsToDefault();
        ActionBindings = new ReadOnlyObservableCollection<InputManager.ActionBindings>(InputManager.Instance.Bindings);
    }
    [RelayCommand] private void OkPressed()
    {
        UserSettings prefs = new UserSettings()
        {
            MouseSensitivity = MouseSensitivity,
            ControllerStickSensitivity = ControllerStickSensitivity,
            ScrollSpeed = ScrollSpeed,
            CropResizeToMax = CropResizeToMax,
            HideCursorTime = HideCursorTime,
            IvpAnimSpeed = IvpAnimSpeed,
            IvpAnimationType = IvpAnimType,
            TurnPageBouncingTime = TurnpageBouncingTime,
            ShowPagingInfo = ShowPagingInfo,
            ShowAlbumPath = ShowAlbumPath,
            ShowPageName = ShowPageName,
            ShowImageSize = ShowImageSize,
            BookmarkValidity = BookmarkValidity,
            Realesrgan_ncnn_vulkan_path = Realesrgan_ncnn_vulkan_path,
            
        };  
        
        InputManager.Instance.DragPanButton = DragPanBtn;
        InputManager.Instance.DragZoomButton = DragZoomBtn;
        InputManager.Instance.DrawZoomRectButton = DrawZoomRectBtn;
        InputManager.Instance.PanStick = StickPan;
        InputManager.Instance.ZoomStick = StickZoom;
        InputManager.Instance.StickInverter = StickInverter;
        

        _window?.Close(prefs); // Return the result from ShowDialog
    }
}