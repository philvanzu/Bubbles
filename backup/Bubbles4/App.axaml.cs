using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
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
                desktop.MainWindow = new MainWindow
                {
                    DataContext = mvm
                };
                // Fire-and-forget safely
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await mvm.InitializeAsync(libraryPath);
                    }
                    catch (Exception ex)
                    {
                        // Optional: handle/log any init errors
                        Console.WriteLine($"Initialization failed: {ex.Message}");
                    }
                });
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