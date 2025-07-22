using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Bubbles4.Models;
using Bubbles4.ViewModels;
using Bubbles4.Views;
using Bubbles4.Services;
namespace Bubbles4;

public class App : Application
{
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            var dialogService = new DialogService();
            if (desktop.Args != null)
            {
                string? libraryPath = desktop.Args.FirstOrDefault();
                var appState = AppStorage.Instance.AppState;
                
                var mainWindow = new MainWindow
                {
                    Position = appState.WindowPosition,
                    Width = appState.WindowWidth,
                    Height = appState.WindowHeight,
                    WindowState = appState.WindowState
                };
                var mvm = new MainViewModel(mainWindow, dialogService);
                mainWindow.DataContext = mvm;
                InputManager.MainViewModel = mvm;
                
  
                mvm.MainWindow = mainWindow;
                mainWindow.Opened += (_, _) =>
                {
                    Dispatcher.UIThread.Post(async void() =>
                    {
                        try
                        {
                            InputManager.Instance.Initialize();
                            await Task.Delay(1000);
                            mvm.Initialize(libraryPath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Initialization failed: {ex}");
                        }    
                    });
                };
                mainWindow.Closing += (_, e) =>
                {
                    mvm.OnShutdown();
                    if (mvm.ShutdownCoordinator.IsShutdownBlocked)
                    {
                        e.Cancel = true;
                        return;
                    }
                    if(mvm.IsFullscreen)mvm.ExitFullScreenCommand.Execute(null);
                    AppStorage.Instance.Save();
                };
                mainWindow.Closed += (_, _) =>
                {
                    try
                    {
                        var appData = AppStorage.Instance;
                        var state = appData.AppState;
                        state.WindowPosition = mainWindow.Position;
                        state.WindowWidth = mainWindow.Width;
                        state.WindowHeight = mainWindow.Height;
                        state.WindowState = mainWindow.WindowState;
                        appData.AppState = state;
                        appData.Save();
                    }
                    catch (Exception ex){Console.WriteLine(ex);}
                };

                desktop.MainWindow = mainWindow;
            }

            
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}