using System;

namespace Bubbles4.Models;

public interface ISelectItems
{
    public event EventHandler<SelectedItemChangedEventArgs>? SelectionChanged;
    public event EventHandler? SortOrderChanged;
    public event EventHandler<int>? ScrollToIndexRequested;
    public void InvokeSelectionChanged(ISelectableItem? newItem, ISelectableItem? oldItem);
    public void InvokeSortOrderChanged();
    public void RequestScrollToIndex(int index);

    public int GetSelectedIndex();
}

public interface ISelectableItem
{
    public bool IsSelected {get;set;}
}

public class SelectedItemChangedEventArgs : EventArgs
{
    public ISelectItems Sender { get; }
    public ISelectableItem? NewItem { get; }
    public ISelectableItem? OldItem { get; }
    public SelectedItemChangedEventArgs(ISelectItems sender, ISelectableItem? newItem, ISelectableItem? oldItem)
    {
        Sender = sender;
        NewItem = newItem;
        OldItem = oldItem;
    }
}
