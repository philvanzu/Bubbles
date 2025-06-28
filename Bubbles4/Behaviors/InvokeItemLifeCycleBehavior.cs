using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;

namespace Bubbles4.Behaviors;

public class InvokeItemLifecycleBehavior : Behavior<ItemsRepeater>
{
    public static readonly StyledProperty<ICommand?> ItemPreparedCommandProperty =
        AvaloniaProperty.Register<InvokeItemLifecycleBehavior, ICommand?>(nameof(ItemPreparedCommand));

    public static readonly StyledProperty<ICommand?> ItemClearingCommandProperty =
        AvaloniaProperty.Register<InvokeItemLifecycleBehavior, ICommand?>(nameof(ItemClearingCommand));

    public ICommand? ItemPreparedCommand
    {
        get => GetValue(ItemPreparedCommandProperty);
        set => SetValue(ItemPreparedCommandProperty, value);
    }

    public ICommand? ItemClearingCommand
    {
        get => GetValue(ItemClearingCommandProperty);
        set => SetValue(ItemClearingCommandProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject != null)
        {
            AssociatedObject.ElementPrepared += OnElementPrepared;
            AssociatedObject.ElementClearing += OnElementClearing;
        }
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.ElementPrepared -= OnElementPrepared;
            AssociatedObject.ElementClearing -= OnElementClearing;
        }

        base.OnDetaching();
    }

    private void OnElementPrepared(object? sender, ItemsRepeaterElementPreparedEventArgs e)
    {
        //Console.WriteLine($"item prepared{e.Element}  CanExecute : {ItemPreparedCommand?.CanExecute(e.Element?.DataContext)}");
        if (ItemPreparedCommand?.CanExecute(e.Element?.DataContext) == true)
            ItemPreparedCommand.Execute(e.Element?.DataContext);
    }

    private void OnElementClearing(object? sender, ItemsRepeaterElementClearingEventArgs e)
    {
        if (ItemClearingCommand?.CanExecute(e.Element?.DataContext) == true)
            ItemClearingCommand.Execute(e.Element?.DataContext);
    }
}