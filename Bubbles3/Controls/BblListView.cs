using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace Bubbles3.Controls
{
    public class BblListView : ListView
    {
        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            //Scroll to selection when changed.
            base.OnSelectionChanged(e);
            try
            {
                ScrollIntoView(SelectedItem);
            }
            catch (Exception x) { Console.WriteLine(x.Message); }

        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            //prevent right click selection
            if (e.ChangedButton == MouseButton.Right) e.Handled = true;
            else base.OnPreviewMouseDown(e);
        }
    }

}
