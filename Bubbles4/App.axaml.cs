using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
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

public partial class App : Application
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
                var mvm = new MainViewModel(dialogService);
                var appState = AppStorage.Instance.AppState;
                var mainWindow = new MainWindow
                {
                    DataContext = mvm,
                    Position = appState.WindowPosition,
                    Width = appState.WindowWidth,
                    Height = appState.WindowHeight,
                    WindowState = appState.WindowState
                };
                mainWindow.Opened += (sender, e) =>
                {
                    Dispatcher.UIThread.Post(async () =>
                    {
                        try
                        {
                            await Task.Delay(1000);
                            mvm.Initialize(libraryPath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Initialization failed: {ex}");
                        }    
                    });
                };
                mainWindow.Closed += (sender, e) =>
                {
                    try
                    {
                        var appData = AppStorage.Instance;
                        var appState = appData.AppState;
                        appState.WindowPosition = mainWindow.Position;
                        appState.WindowWidth = mainWindow.Width;
                        appState.WindowHeight = mainWindow.Height;
                        appState.WindowState = mainWindow.WindowState;
                        appData.AppState = appState;
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