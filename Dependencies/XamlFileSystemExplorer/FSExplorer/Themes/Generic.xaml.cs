using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using XamlFSExplorer.Utils;
using XamlFSExplorer;

namespace XamlFSExplorer
{
    public partial class Generic : ResourceDictionary
    {
        //++++++++++++++++++++
        // TREEVIEW stuff
        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }

        //Routing events to their intended handler in a resource dictionary. Is it possible any other way?
        public void OnTVPreviewKeyDown(object sender, KeyEventArgs e)
        {
            TreeView tv = sender as TreeView;
            var tree = DependencyObjectHelper.FindAncestorType<FSExplorerTree>(tv);
            if(tree != null && tree is FSExplorerTree)
                if(tree.Explorer != null) tree.Explorer.OnTVPreviewKeyDown(sender, e);
        }

        FSExplorerItem GetTreeExplorerItem(DependencyObject d)
        {
            var ti = DependencyObjectHelper.FindAncestorType<ExplorerTreeViewItem>(d);
            if (ti == null || ti.HasHeader == false || ti.Header == null) return null;

            return ti.Header as FSExplorerItem;
        }

        // ++++++++++++++++++++++++
        //   ListView Stuff
        public void LItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListViewItem li && li.Content is FSExplorerItem explorerItem)
                explorerItem.MouseDoubleClick(sender, e);
        }

        public void OnLVPreviewKeyDown(object sender, KeyEventArgs e)
        {
            ListView lv = sender as ListView;
            var list = DependencyObjectHelper.FindAncestorType<FSExplorerList>(lv);
            if (list != null && list is FSExplorerList)
                if (list.Explorer != null) list.OnLVPreviewKeyDown(sender, e);
        }

        FSExplorerItem GetListExplorerItem(DependencyObject d)
        {
            var li = DependencyObjectHelper.FindAncestorType<ListViewItem>(d);
            if (li == null || li.Content == null) return null;

            return li.Content as FSExplorerItem;
        }

        void ColumnHeaderClicked(object sender, EventArgs e)
        {
            var ch = sender as GridViewColumnHeader;
            var list = DependencyObjectHelper.FindAncestorType<FSExplorerList>(ch);
            list.ListColumnHeaderClicked((string)ch.Content);
        }
        
        // ++++++++++++++
        //Navbar stuff
        private void AddressBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if(sender is Border bd && bd.DataContext is FSExplorerNavbar nb)
            {
                nb.OnAddressBarClicked(sender, e);

            }
        }
        private void AddressBar_LostFocus(object sender, EventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is FSExplorerNavbar nb)
            {
                nb.AddressBarLostFocus(sender, e);
            }
        }

        // ++++++++++++++
        // Neutral stuff
        public void Item_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var sp = sender as StackPanel;
            var fromTree = sp.Name == "ItemStackPanelTree";
            var explorerItem = (fromTree)? GetTreeExplorerItem(sp) : GetListExplorerItem(sp);
            if (explorerItem != null) explorerItem.PreviewMouseLeftButtonDown(sender, e, fromTree);
        }
        public void Item_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var sp = sender as StackPanel;
            var fromTree = sp.Name == "ItemStackPanelTree";
            var explorerItem = (fromTree) ? GetTreeExplorerItem(sp) : GetListExplorerItem(sp);
            if (explorerItem != null) explorerItem.PreviewMouseLeftButtonUp(sender, e, fromTree);
        }
        public void Item_MouseMove(object sender, MouseEventArgs e)
        {
            var sp = sender as StackPanel;
            var fromTree = sp.Name == "ItemStackPanelTree";
            var explorerItem = (fromTree) ? GetTreeExplorerItem(sp) : GetListExplorerItem(sp);
            if (explorerItem != null) explorerItem.MouseMove(sender, e, fromTree);
        }
        public void Item_MouseEnter(object sender, MouseEventArgs e)
        {
            var sp = sender as StackPanel;
            var fromTree = sp.Name == "ItemStackPanelTree";
            var explorerItem = (fromTree) ? GetTreeExplorerItem(sp) : GetListExplorerItem(sp);
            if (explorerItem != null) explorerItem.MouseEnter(sender, e, fromTree);
        }
        public void Item_MouseLeave(object sender, MouseEventArgs e)
        {
            var sp = sender as StackPanel;
            var fromTree = sp.Name == "ItemStackPanelTree";
            var explorerItem = (fromTree) ? GetTreeExplorerItem(sp) : GetListExplorerItem(sp);
            if (explorerItem != null) explorerItem.MouseLeave(sender, e, fromTree);
        }
        public void Item_DragEnter(object sender, DragEventArgs e)
        {
            var sp = sender as StackPanel;
            var fromTree = sp.Name == "ItemStackPanelTree";
            var explorerItem = (fromTree) ? GetTreeExplorerItem(sp) : GetListExplorerItem(sp);
            if (explorerItem != null) explorerItem.DragEnter(sender, e, fromTree);
        }
        public void Item_DragOver(object sender, DragEventArgs e)
        {
            var sp = sender as StackPanel;
            var fromTree = sp.Name == "ItemStackPanelTree";
            var explorerItem = (fromTree) ? GetTreeExplorerItem(sp) : GetListExplorerItem(sp);
            if (explorerItem != null) explorerItem.DragOver(sender, e, fromTree);
        }
        public void Item_DragLeave(object sender, DragEventArgs e)
        {
            var sp = sender as StackPanel;
            var fromTree = sp.Name == "ItemStackPanelTree";
            var explorerItem = (fromTree) ? GetTreeExplorerItem(sp) : GetListExplorerItem(sp);
            if (explorerItem != null) explorerItem.DragLeave(sender, e, fromTree);
        }
        public void Item_Drop(object sender, DragEventArgs e)
        {
            var sp = sender as StackPanel;
            var fromTree = sp.Name == "ItemStackPanelTree";
            var explorerItem = (fromTree) ? GetTreeExplorerItem(sp) : GetListExplorerItem(sp);
            if (explorerItem != null) explorerItem.Drop(sender, e, fromTree);
        }
        public void Item_RenameTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            var tb = sender as TextBox;
            var explorerItem = (tb.Name == "NameTextBoxTree") ? GetTreeExplorerItem(tb) : GetListExplorerItem(tb);
            if (explorerItem != null) explorerItem.RenameTextBoxKeyDown(sender, e);
        }
        public void TVI_RenameTextBoxLostFocus(object sender, EventArgs e)
        {
            var tb = sender as TextBox;
            var explorerItem = (tb.Name == "NameTextBoxTree") ? GetTreeExplorerItem(tb) : GetListExplorerItem(tb);
            if (explorerItem != null) explorerItem.RenameTextBoxLostFocus();
        }



    }

    public class FSTreeViewDataTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var myResourceDictionary = new ResourceDictionary();
            var uriPath = string.Format("/{0};component/Themes/Generic.xaml", typeof(FSTreeViewDataTemplateSelector).Assembly.GetName().Name);
            var uri = new Uri(uriPath, UriKind.RelativeOrAbsolute);
            myResourceDictionary.Source = uri;

            //FSExplorerItem explorerItem = item as FSExplorerItem;

            //var rootcontrol = container;
            //FSExplorerTreeView tvRoot = null;
            //while(true)
            //{
            //    if (!(rootcontrol is Visual || rootcontrol is Visual3D))
            //        break;

            //    if(rootcontrol is FSExplorerTreeView)
            //    {
            //        tvRoot = rootcontrol as FSExplorerTreeView;
            //        break;
            //    }
            //    else
            //    {
            //        rootcontrol = VisualTreeHelper.GetParent(rootcontrol);
            //        if (rootcontrol == null) break;
            //        continue;
            //    }
            //}

            return myResourceDictionary["FilesExplorerTreeviewDataTemplate"] as DataTemplate;
        }


    }
}
