using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using XamlFSExplorer.Utils;

namespace XamlFSExplorer
{
    class ExplorerListView : ListView
    {

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            var dpd = DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(ListView));
            if (dpd != null)
            {
                dpd.AddValueChanged(this, OnItemsSourceChanged);
            }
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);

            var list = DataContext as FSExplorerList;
            var lvi = element as ListViewItem;
            var ei = item as FSExplorerItem;
            if (list?.ShowFiles != true && ei != null && !ei.IsDirectory) lvi.Visibility = Visibility.Collapsed;
            else lvi.Visibility = ei.Visibility;
        }

        IEnumerable _itemsCollection;
        private void OnItemsSourceChanged(object sender, EventArgs e)
        {
            if(_itemsCollection != null) ((INotifyCollectionChanged)_itemsCollection).CollectionChanged -= CollectionChanged;
            ((INotifyCollectionChanged) ItemsSource).CollectionChanged += CollectionChanged;
            _itemsCollection = ItemsSource;
        }

        public void CollectionChanged(Object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
                (View as ExplorerGridView).OnCollectionReset();
            if(e.Action == NotifyCollectionChangedAction.Add)
                foreach(var item in e.NewItems)
                (View as ExplorerGridView).ItemAdded(item);
        }
    }
}
