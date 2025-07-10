using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using System.Windows.Input;

namespace Bubbles4.Behaviors;
public class PointerPressedCommandBehavior : Behavior<Control>
{
    


    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PointerPressedCommandBehavior, ICommand?>(nameof(Command));

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject != null)
            AssociatedObject.PointerPressed += OnPointerPressed;
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
            AssociatedObject.PointerPressed -= OnPointerPressed;

        base.OnDetaching();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var parameter = AssociatedObject?.DataContext;
        if (Command?.CanExecute(parameter) == true)
            Command.Execute(parameter);
    }

}
