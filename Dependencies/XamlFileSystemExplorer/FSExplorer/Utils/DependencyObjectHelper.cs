using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace XamlFSExplorer.Utils
{
    public static class DependencyObjectHelper
    {
        public static DependencyObject GetParent(DependencyObject child)
        {
            //get parent item
            return VisualTreeHelper.GetParent(child);
        }

        public static T FindAncestorType<T>(DependencyObject o) where T : DependencyObject
        {
            T item = null;
            var parent = o;

            while (item == null && parent != null)
            {
                parent = VisualTreeHelper.GetParent(parent as DependencyObject);
                if (parent == null) break;
                item = parent as T;
            }

            return item;
        }

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

    }
}
