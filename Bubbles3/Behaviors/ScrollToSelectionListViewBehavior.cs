using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interactivity;

namespace Bubbles3.Behaviors
{
    public class ScrollToSelectionListViewBehaviors : Behavior<ListView>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.AddHandler(ListView.SelectionChangedEvent, new RoutedEventHandler(AssociatedObject_SelectionChanged), true);
        }

        void AssociatedObject_SelectionChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {

                AssociatedObject.ScrollIntoView(AssociatedObject.SelectedItem);
            }
            catch (Exception ex) {
                //TODO : trips after LibraryViewModel.OnLibraryLoaded, trying to moveto bookmarked book.
    //at System.Windows.Data.ListCollectionView.get_InternalCount()\r\n
    //at System.Windows.Data.ListCollectionView.get_IsEmpty()\r\n
    //at System.Windows.Data.CollectionView.SetCurrent(Object newItem, Int32 newPosition)\r\n
    //at System.Windows.Data.ListCollectionView.RefreshOverride()\r\n
    //at System.Windows.Data.CollectionView.RefreshInternal()\r\n
    //at System.Windows.Data.CollectionView.EndDefer()\r\n
    //at System.Windows.Data.CollectionView.DeferHelper.Dispose()\r\n
    //at System.Windows.Controls.ItemCollection.SetCollectionView(CollectionView view)\r\n
    //at System.Windows.Controls.ItemsControl.OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)\r\n
    //at System.Windows.DependencyObject.OnPropertyChanged(DependencyPropertyChangedEventArgs e)\r\n
    //at System.Windows.FrameworkElement.OnPropertyChanged(DependencyPropertyChangedEventArgs e)\r\n
    //at System.Windows.DependencyObject.NotifyPropertyChange(DependencyPropertyChangedEventArgs args)\r\n
    //at System.Windows.DependencyObject.UpdateEffectiveValue(EntryIndex entryIndex, DependencyProperty dp, PropertyMetadata metadata, EffectiveValueEntry oldEntry, EffectiveValueEntry & newEntry, Boolean coerceWithDeferredReference, Boolean coerceWithCurrentValue, OperationType operationType)\r\n
    // at System.Windows.DependencyObject.InvalidateProperty(DependencyProperty dp, Boolean preserveCurrentValue)\r\n
    // at System.Windows.Data.BindingExpressionBase.Invalidate(Boolean isASubPropertyChange)\r\n
    // at System.Windows.Data.BindingExpression.TransferValue(Object newValue, Boolean isASubPropertyChange)\r\n
    // at System.Windows.Data.BindingExpression.Activate(Object item)\r\n at System.Windows.Data.BindingExpression.AttachToContext(AttachAttempt attempt)\r\n
    // at System.Windows.Data.BindingExpression.MS.Internal.Data.IDataBindEngineClient.AttachToContext(Boolean lastChance)\r\n
    // at MS.Internal.Data.DataBindEngine.Task.Run(Boolean lastChance)\r\n at MS.Internal.Data.DataBindEngine.Run(Object arg)\r\n
    // at System.Windows.ContextLayoutManager.fireLayoutUpdateEvent()\r\n at System.Windows.ContextLayoutManager.UpdateLayout()\r\n
    // at System.Windows.Controls.ItemsControl.OnBringItemIntoView(ItemInfo info)\r\n
    // at Bubbles3.Behaviors.ScrollToSelectionListViewBehavior.AssociatedObject_SelectionChanged(Object sender, RoutedEventArgs e) in D:\\dev\\Bubbles3\\Bubbles3\\Behaviors\\ScrollToSelectionListViewBehavior.cs:line 24"
            }
        }



        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.RemoveHandler(ListView.SelectionChangedEvent, new RoutedEventHandler(AssociatedObject_SelectionChanged));
        }
    }
}
