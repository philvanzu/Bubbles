using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Input;
using System.ComponentModel;
using System.Collections;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Globalization;
using System.IO;

namespace XamlFSExplorer
{
    internal class ExplorerTreeView : TreeView, INotifyPropertyChanged
    {

        #region fields
        private ExplorerTreeViewItem _selectTreeViewItemOnMouseUp;
        #endregion

        #region DependencyProperties + Wrappers + ChangedHandlers
        //++++++++++++++++++++++++++++++++++++
        //AltSelectedItem DependencyProperty
        public static DependencyProperty AltSelectedItemProperty =
            DependencyProperty.Register("AltSelectedItem", typeof(FileSystemInfoEx), typeof(ExplorerTreeView), new FrameworkPropertyMetadata(null, OnAltSelectedItemChanged) { BindsTwoWayByDefault = true });

        public FileSystemInfoEx AltSelectedItem
        {
            get { return (FileSystemInfoEx)GetValue(AltSelectedItemProperty); }
            set {
                SetValue(AltSelectedItemProperty, value);
                NotifyPropertyChanged("AltSelectedItem");
            }
        }

        public static object GetAltSelectedItem(ExplorerTreeView element)
        {
            if (element == null) return null;
            return (object)element.GetValue(AltSelectedItemProperty);
        }

        public static void SetAltSelectedItem(ExplorerTreeView element, object value)
        {
            if (element == null) return;
            if (value is FSExplorerItem ei) value = ei.Info;
            if (element.GetValue(AltSelectedItemProperty) != value)
            {
                element.SetValue(AltSelectedItemProperty, value);
                element.NotifyPropertyChanged("AltSelectedItem");
            }
        }

        private static void OnAltSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var treeViewItem = d as ExplorerTreeViewItem;
            var treeView = FindTreeView(treeViewItem);
            if (treeViewItem != null && treeView != null)
            {
                var selectedItem = GetAltSelectedItem(treeView);
                if (selectedItem != null)
                {
                    if (ExplorerTreeViewItem.GetIsAltSelected(treeViewItem))
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

        
        #endregion




        #region Events & Handlers

        public event PropertyChangedEventHandler PropertyChanged;
        void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new ExplorerTreeViewItem();
        }

        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return item is ExplorerTreeViewItem;
        }


        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
        
            if (e.OriginalSource is ExplorerTreeView) return;

            var treeViewItem = FindTreeViewItem(e.OriginalSource as DependencyObject);
            if (treeViewItem == null) return;

            if (Mouse.LeftButton == MouseButtonState.Pressed && ExplorerTreeViewItem.GetIsAltSelected(treeViewItem))
                _selectTreeViewItemOnMouseUp = treeViewItem;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
        }

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);
        
            var treeViewItem = FindTreeViewItem(e.OriginalSource as DependencyObject);
            if (treeViewItem != null) _selectTreeViewItemOnMouseUp = treeViewItem;
        }

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseRightButtonDown(e);
        
            var treeViewItem = FindTreeViewItem(e.OriginalSource as DependencyObject);
            if (treeViewItem == _selectTreeViewItemOnMouseUp) ExplorerTreeViewItem.SetIsAltSelected(treeViewItem, true, this);
        }
        #endregion

        #region static finders
        public static ExplorerTreeView FindTreeView(DependencyObject dependencyObject)
        {
            if (dependencyObject == null) return null;

            var treeView = dependencyObject as ExplorerTreeView;
            return treeView ?? FindTreeView(VisualTreeHelper.GetParent(dependencyObject));
        }

        public static ExplorerTreeViewItem FindTreeViewItem(DependencyObject dependencyObject)
        {
            if (!(dependencyObject is Visual || dependencyObject is Visual3D))
                return null;

            var checkbox = dependencyObject as CheckBox;
            var expander = dependencyObject as ToggleButton;
            if (checkbox != null || expander != null) return null;

            if (dependencyObject is ExplorerTreeViewItem treeViewItem) return treeViewItem;
            return FindTreeViewItem(VisualTreeHelper.GetParent(dependencyObject));
        }
        #endregion


        #region Selection logic
        public static void AltDeselectItem(ExplorerTreeView treeView, ExplorerTreeViewItem treeViewItem, ExplorerTreeViewItem skip)
        {
            if (treeView != null)
            {
                for (int i = 0; i < treeView.Items.Count; i++)
                {
                    if (treeView.ItemContainerGenerator.ContainerFromIndex(i) is ExplorerTreeViewItem item)
                    {
                        if (item != skip) ExplorerTreeViewItem.SetIsAltSelected(item, false, treeView);
                        AltDeselectItem(null, item, skip);
                    }
                }
            }
            else
            {
                for (int i = 0; i < treeViewItem.Items.Count; i++)
                {
                    if (treeViewItem.ItemContainerGenerator.ContainerFromIndex(i) is ExplorerTreeViewItem item)
                    {
                        if (item != skip) ExplorerTreeViewItem.SetIsAltSelected(item, false, treeView);
                        AltDeselectItem(null, item, skip);
                    }
                }
            }
        }

        #endregion


    }
}

