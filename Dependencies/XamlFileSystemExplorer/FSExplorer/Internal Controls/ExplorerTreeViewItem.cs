using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace XamlFSExplorer
{
    internal class ExplorerTreeViewItem:TreeViewItem, INotifyPropertyChanged
    {

        //++++++++++++++++++++++++++++++++++
        //IsAltSelected DependencyProperty
        public static DependencyProperty IsAltSelectedProperty =
            DependencyProperty.RegisterAttached("IsAltSelected", typeof(bool), typeof(ExplorerTreeViewItem), new FrameworkPropertyMetadata(false, OnIsAltSelectedChanged) { BindsTwoWayByDefault = true });



        public bool IsAltSelected
        {
            get { return (bool)GetValue(IsAltSelectedProperty); }
            set {
                SetValue(IsAltSelectedProperty, value);
                NotifyPropertyChanged("IsAltSelected");
            }
        }


        public static bool GetIsAltSelected(ExplorerTreeViewItem element)
        {
            return (bool)element.GetValue(IsAltSelectedProperty);
        }

        public static void SetIsAltSelected(ExplorerTreeViewItem element, Boolean value, ExplorerTreeView treeView)
        {
            if (element == null) return;

            if (value)
            {
                //deselect previous selection
                var selected = ExplorerTreeView.GetAltSelectedItem(treeView);
                ExplorerTreeView.AltDeselectItem(treeView, element, element);
            }

            if (GetIsAltSelected(element) != value)
            {
                element.SetValue(IsAltSelectedProperty, value);
                element.NotifyPropertyChanged("IsAltSelected");
            }
            object selectedobj = ExplorerTreeView.GetAltSelectedItem(treeView);
            if (selectedobj != null && selectedobj.Equals(element.Header)) return;

            ExplorerTreeView.SetAltSelectedItem(treeView, value ? element.Header : null);
        }

        private static void OnIsAltSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var treeViewItem = d as ExplorerTreeViewItem;
            var treeView = ExplorerTreeView.FindTreeView(treeViewItem);
            if (treeViewItem != null && treeView != null)
            {
                //SetIsAltSelected(treeViewItem, GetIsAltSelected(treeViewItem), treeView);
            }
        }


        public int Depth
        {
            get
            {
                ExplorerTreeViewItem parent;
                while ((parent = GetParent()) != null)
                {
                    return (parent.Depth) + 1;
                }
                return 0;
            }
        }

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

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);

            var ei = item as FSExplorerItem;
            var rootcontrol = element;
            FSExplorerTree tvRoot = null;
            while (true)
            {
                if (!(rootcontrol is Visual || rootcontrol is Visual3D))
                    break;

                if (rootcontrol is FSExplorerTree)
                {
                    tvRoot = rootcontrol as FSExplorerTree;
                    break;
                }
                else
                {
                    rootcontrol = VisualTreeHelper.GetParent(rootcontrol);
                    if (rootcontrol == null) break;
                    continue;
                }
            }

            ExplorerTreeViewItem tvi = element as ExplorerTreeViewItem;
            if (tvRoot != null && tvRoot.ShowFiles == false && ei != null && !ei.IsDirectory) tvi.Visibility = Visibility.Collapsed;
            //else tvi.Visibility = ei.Visibility;
        }

        
        private ExplorerTreeViewItem GetParent()
        {
            var parent = VisualTreeHelper.GetParent(this);
            while (!(parent is ExplorerTreeViewItem || parent is TreeView))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as ExplorerTreeViewItem;
        }

    }
}

