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
    

    [RelayCommand] public void OkPressed()
    {
        Preferences prefs = new Preferences()
        {
            MouseSensitivity = MouseSensitivity,
            ControllerStickSensitivity = ControllerStickSensitivity,
            CacheLibraryData = CacheLibraryData,
        };    
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
        {
            var window = app.Windows.FirstOrDefault(w => w.DataContext == this);
            window?.Close(prefs); // Return the result from ShowDialog
        }
    }
}