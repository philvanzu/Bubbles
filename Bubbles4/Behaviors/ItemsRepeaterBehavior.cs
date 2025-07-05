using System;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using Avalonia.Threading;
using Bubbles4.ViewModels;

namespace Bubbles4.Behaviors
{
    public class ItemsRepeaterBehavior : Behavior<ItemsRepeater>
    {
        public static bool SuppressNextAutoScroll;

        private ViewModelBase? _viewModel;
        private int _lastScrolledIndex = -1;
        private bool _isScrollPending;
        
        public static readonly StyledProperty<ICommand?> ItemPreparedCommandProperty =
            AvaloniaProperty.Register<ItemsRepeaterBehavior, ICommand?>(nameof(ItemPreparedCommand));

        public static readonly StyledProperty<ICommand?> ItemClearingCommandProperty =
            AvaloniaProperty.Register<ItemsRepeaterBehavior, ICommand?>(nameof(ItemClearingCommand));

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

        // Add any autoscroll properties you had here as StyledProperties if needed

        protected override void OnAttached()
        {
            base.OnAttached();
            if (AssociatedObject != null)
            {
                AssociatedObject.ElementPrepared += OnElementPrepared;
                AssociatedObject.ElementClearing += OnElementClearing;
                
                // Observe DataContext changes explicitly
                AssociatedObject.GetObservable(Control.DataContextProperty)
                    .Subscribe(dc => TryHookIntoViewModel(dc));

                // Call once immediately in case DataContext is already set
                TryHookIntoViewModel(AssociatedObject.DataContext);

                // Subscribe to any events needed for autoscroll here
            }
        }
        protected override void OnDetaching()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.ElementPrepared -= OnElementPrepared;
                AssociatedObject.ElementClearing -= OnElementClearing;

                // Unsubscribe autoscroll events here
            }
            base.OnDetaching();
        }

        private void OnElementPrepared(object? sender, ItemsRepeaterElementPreparedEventArgs e)
        {
            if (ItemPreparedCommand?.CanExecute(e.Element?.DataContext) == true)
            {
                var context = e.Element?.DataContext;
                Dispatcher.UIThread.Post(() =>
                {
                    if (ItemPreparedCommand.CanExecute(context))
                        ItemPreparedCommand.Execute(context);
                }, DispatcherPriority.Background);
            }
        }

        private void OnElementClearing(object? sender, ItemsRepeaterElementClearingEventArgs e)
        {
            if (ItemClearingCommand?.CanExecute(e.Element?.DataContext) == true)
            {
                var context = e.Element?.DataContext;
                Dispatcher.UIThread.Post(() =>
                {
                    if (ItemClearingCommand.CanExecute(context))
                        ItemClearingCommand.Execute(context);
                }, DispatcherPriority.Background);
            }
        }
        private void TryHookIntoViewModel(object? dc)
        {
            if (_viewModel is INotifyPropertyChanged oldVm)
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;

            _viewModel = dc as ViewModelBase;

            if (_viewModel is INotifyPropertyChanged newVm)
                newVm.PropertyChanged += OnViewModelPropertyChanged;
        }
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            int index = -1;

            if (e.PropertyName == nameof(LibraryViewModel.SelectedItem))
            {
                if (_viewModel is LibraryViewModel libvm)
                {
                    index = libvm.GetBookIndex(libvm.SelectedItem);
                }
            }
            else if (e.PropertyName == nameof(BookViewModel.SelectedPage))
            {
                if (_viewModel is BookViewModel bookvm)
                {
                    index = bookvm.GetPageIndex(bookvm.SelectedPage);
                }
            }

            if (index != -1)
                ScheduleScrollIntoView(index);
        }
        private void ScheduleScrollIntoView(int index)
        {
            if (SuppressNextAutoScroll)
            {
                SuppressNextAutoScroll = false;
                return;
            }

            // Skip if scrolling to same index again
            if (_lastScrolledIndex == index)
                return;

            // Optionally, skip autoscroll for item zero if it's problematic
            // if (index == 0) return;

            if (_isScrollPending)
                return;

            _isScrollPending = true;

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                ScrollIntoView(index);
                _lastScrolledIndex = index;
                _isScrollPending = false;
            }, DispatcherPriority.Background);
        }

        private void ScrollIntoView(int index)
        {
            var repeater = AssociatedObject;
            if (repeater == null)
                return;

            var container = repeater.TryGetElement(index);
            if (container != null)
            {
                container.BringIntoView();
            }
        }
        
        
    }
}
