using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using Bubbles4.ViewModels;

namespace Bubbles4.Behaviors;

public class InvokeOnLoadBehavior:Behavior<Control>
{
    public static readonly StyledProperty<ICommand?> LoadedCommandProperty =
        AvaloniaProperty.Register<InvokeOnLoadBehavior, ICommand?>(nameof(LoadedCommand));

    public static readonly StyledProperty<ICommand?> UnloadedCommandProperty =
        AvaloniaProperty.Register<InvokeOnLoadBehavior, ICommand?>(nameof(UnloadedCommand));

    public ICommand? LoadedCommand
    {
        get => GetValue(LoadedCommandProperty);
        set => SetValue(LoadedCommandProperty, value);
    }

    public ICommand? UnloadedCommand
    {
        get => GetValue(UnloadedCommandProperty);
        set => SetValue(UnloadedCommandProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject != null)
        {
            AssociatedObject.AttachedToVisualTree += OnAttachedToVisualTree;
            AssociatedObject.DetachedFromVisualTree += OnDetachedFromVisualTree;
        }
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.AttachedToVisualTree -= OnAttachedToVisualTree;
            AssociatedObject.DetachedFromVisualTree -= OnDetachedFromVisualTree;
        }
        base.OnDetaching();
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (AssociatedObject?.DataContext is ViewModelBase vm && LoadedCommand?.CanExecute(vm) == true)
        {
            LoadedCommand.Execute(vm);
        }
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (AssociatedObject?.DataContext is ViewModelBase vm && UnloadedCommand?.CanExecute(vm) == true)
        {
            UnloadedCommand.Execute(vm);
        }
    }
}
