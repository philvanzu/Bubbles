using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Bubbles4.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bubbles4.ViewModels;

public partial class PreferencesEditorViewModel: ViewModelBase
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

    public PreferencesEditorViewModel()
    {
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
    }
    
    [RelayCommand] public void OkPressed()
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
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
        {
            var window = app.Windows.FirstOrDefault(w => w.DataContext == this);
            window?.Close(prefs); // Return the result from ShowDialog
        }
    }
}