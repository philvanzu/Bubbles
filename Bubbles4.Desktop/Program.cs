using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Skia;
using Avalonia.Win32;

namespace Bubbles4.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder  =  AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            builder = builder
                .With(new SkiaOptions
                {
                    // No UseGpu in Avalonia 11. GPU backend is used automatically if available.
                    MaxGpuResourceSizeBytes = 256 * 1024 * 1024,
                });
        }
        return builder;
    }
        
       
}