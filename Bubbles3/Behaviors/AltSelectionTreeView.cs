using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interactivity;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Bubbles3.Behaviors
{
    //Adds two properties to the TreeView:
    // AltSelectedItem : an alternate SelectedItem that will not get changed when a parent node is collapsed.
    // IsAltSelected : is to AltSelectedItem what IsSelected is to SelectedItem.
    // Todo : AltSelectedItem is broken and serves no purpose.
    class AltSelectionTreeView : Behavior<TreeView>
    {
        public static DependencyProperty AltSelectedItemProperty =
            DependencyProperty.RegisterAttached("AltSelectedItem", typeof(object), typeof(AltSelectionTreeView), 
                                                            new FrameworkPropertyMetadata(null, OnAltSelectedItemChanged) { BindsTwoWayByDefault = true });

        //property wrapper
        public object AltSelectedItem
        {
            get { return (object)GetValue(AltSelectedItemProperty); }
            set { SetValue(AltSelectedItemProperty, value); }
        }

        
        public static DependencyProperty IsSelectedProperty =
            DependencyProperty.RegisterAttached("IsSelected", typeof(bool), typeof(AltSelectionTreeView),
                                                            new FrameworkPropertyMetadata(false, OnIsSelectedChanged) { BindsTwoWayByDefault = true });

        public bool IsSelected
        {
            get { return (bool)GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }

        private TreeViewItem _selectTreeViewItemOnMouseUp;
        
        protected override void OnAttached()
        {
            base.OnAttached();
            this.AssociatedObject.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            this.AssociatedObject.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            this.AssociatedObject.GotFocus += OnTreeViewItemGotFocus;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            if (this.AssociatedObject != null)
            {
                this.AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
                this.AssociatedObject.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
                this.AssociatedObject.GotFocus -= OnTreeViewItemGotFocus;
            }
        }

        private static TreeView FindTreeView(DependencyObject dependencyObject)
        {
            if (dependencyObject == null)
            {
                return null;
            }

            var treeView = dependencyObject as TreeView;

            return treeView ?? FindTreeView(VisualTreeHelper.GetParent(dependencyObject));
        }

        private static TreeViewItem FindTreeViewItem(DependencyObject dependencyObject)
        {
            if (!(dependencyObject is Visual || dependencyObject is Visual3D))
                return null;

            var expander = dependencyObject as ToggleButton;
            if (expander != null)
                return null;

            var treeViewItem = dependencyObject as TreeViewItem;
            if (treeViewItem != null)
            {
                return treeViewItem;
            }

            return FindTreeViewItem(VisualTreeHelper.GetParent(dependencyObject));
        }



        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

            var treeViewItem = FindTreeViewItem(e.OriginalSource as DependencyObject);

            if (treeViewItem != null)// && treeViewItem.IsFocused)
                _selectTreeViewItemOnMouseUp = treeViewItem;
            //OnTreeViewItemGotFocus(sender, e);
        }

        private void OnTreeViewItemGotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            //_selectTreeViewItemOnMouseUp = null;

            if (e.OriginalSource is TreeView) return;

            var treeViewItem = FindTreeViewItem(e.OriginalSource as DependencyObject);
            if (Mouse.LeftButton == MouseButtonState.Pressed && GetIsAltSelected(treeViewItem))
            {
                _selectTreeViewItemOnMouseUp = treeViewItem;
                return;
            }

            //AltSelectItem(treeViewItem, sender as TreeView);
        }

        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var treeViewItem = FindTreeViewItem(e.OriginalSource as DependencyObject);

            if (treeViewItem == _selectTreeViewItemOnMouseUp)
            {
                SetIsAltSelected(treeViewItem, true, sender as TreeView);
            }
        }

        private static void AltDeselectItem(TreeView treeView, TreeViewItem treeViewItem, TreeViewItem skip)
        {
            if (treeView != null)
            {
                for (int i = 0; i < treeView.Items.Count; i++)
                {
                    var item = treeView.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                    if (item != null)
                    {
                        if(item != skip) SetIsAltSelected(item, false, treeView);
                        AltDeselectItem(null, item, skip);
                    }
                }
            }
            else
            {
                for (int i = 0; i < treeViewItem.Items.Count; i++)
                {
                    var item = treeViewItem.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                    if (item != null)
                    {
                        if(item != skip) SetIsAltSelected(item, false, treeView);
                        AltDeselectItem(null, item, skip);
                    }
                }
            }
        }

        public static bool GetIsAltSelected(TreeViewItem element)
        {
            return (bool)element.GetValue(IsSelectedProperty);
        }

        public static void SetIsAltSelected(TreeViewItem element, Boolean value, TreeView treeView)
        {
            if (element == null) return;

            if (value)
            {
                //deselect previous selection
                var selected = GetAltSelectedItem(treeView);
                AltDeselectItem(treeView, element, element);
            }

            if(GetIsAltSelected(element) != value )
            { 
                element.SetValue(IsSelectedProperty, value);
                bool checkval = (bool)element.GetValue(IsSelectedProperty);
            }
            object selectedobj = GetAltSelectedItem(treeView);
            if (selectedobj != null && selectedobj.Equals(element.Header)) return;

            SetAltSelectedItem(treeView, value ? element.Header : null );
        }

        public static object GetAltSelectedItem(TreeView element)
        {
            if (element == null) return null;
            return (object)element.GetValue(AltSelectedItemProperty);
        }

        public static void SetAltSelectedItem(TreeView element, object value)
        {
            if (element == null) return;
            element.SetValue(AltSelectedItemProperty, value);
        }

        private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var treeViewItem = d as TreeViewItem;
            var treeView = FindTreeView(treeViewItem);
            if (treeViewItem != null && treeView != null)
            {
                SetIsAltSelected(treeViewItem, GetIsAltSelected(treeViewItem), treeView);
            }
        }

        private static void OnAltSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var treeViewItem = d as TreeViewItem;
            var treeView = FindTreeView(treeViewItem);
            if (treeViewItem != null && treeView != null)
            {
                var selectedItem = GetAltSelectedItem(treeView);
                if (selectedItem != null)
                {
                    if (GetIsAltSelected(treeViewItem))
                    {
                        SetAltSelectedItem(treeView, treeViewItem.Header);
                    }
                    else
                    {
                        SetAltSelectedItem(treeView, null);
                    }
                }
            }
        }





    }
}
