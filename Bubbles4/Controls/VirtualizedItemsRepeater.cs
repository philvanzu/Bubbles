using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;
using Bubbles4.ViewModels;

namespace Bubbles4.Controls;

public class VirtualizedItemsRepeater : ItemsRepeater
{
    public static readonly StyledProperty<ICommand?> ItemPreparedCommandProperty =
        AvaloniaProperty.Register<VirtualizedItemsRepeater, ICommand?>(nameof(ItemPreparedCommand));

    public static readonly StyledProperty<ICommand?> ItemClearingCommandProperty =
        AvaloniaProperty.Register<VirtualizedItemsRepeater, ICommand?>(nameof(ItemClearingCommand));

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

    /// <summary>
    /// Raised when an item is realized and bound.
    /// </summary>
    public event EventHandler<object?>? ItemPrepared;

    /// <summary>
    /// Raised when an item is unrealized.
    /// </summary>
    public event EventHandler<object?>? ItemCleared;

    public VirtualizedItemsRepeater()
    {
        ElementPrepared += OnElementPreparedInternal;
        ElementClearing += OnElementClearingInternal;
    }

    private void OnElementPreparedInternal(object? sender, ItemsRepeaterElementPreparedEventArgs e)
    {
        var context = e.Element?.DataContext;
        ItemPreparedCommand?.Execute(context);

        ItemPrepared?.Invoke(this, context);
    }

    private void OnElementClearingInternal(object? sender, ItemsRepeaterElementClearingEventArgs e)
    {
        var context = e.Element?.DataContext;
        ItemClearingCommand?.Execute(context);

        ItemCleared?.Invoke(this, context);
    }
}

