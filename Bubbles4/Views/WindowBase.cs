using Avalonia.Controls;
using Bubbles4.Services;

namespace Bubbles4.Views;

public class WindowBase: Window
{
    public WindowBase()
    {
        // Register this window with the AppFocusManager when it's created
        AppFocusManager.Instance.RegisterWindow(this);
    }
}