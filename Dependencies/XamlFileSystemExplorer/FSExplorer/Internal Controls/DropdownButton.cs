using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace XamlFSExplorer
{
    public class DropDownButton : ToggleButton
    {
        public enum Placement { Bottom, Right }
        public Placement DropDownPlacement { private get; set; }
        
        #region DropDown 

        public static readonly DependencyProperty DropDownProperty = 
            DependencyProperty.Register("DropDown", typeof(ContextMenu), typeof(DropDownButton), new PropertyMetadata(null, OnDropDownChanged));

        public ContextMenu DropDown
        {
            get { return (ContextMenu)GetValue(DropDownProperty); }
            set { SetValue(DropDownProperty, value); }
        }

        private static void OnDropDownChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            ((DropDownButton)sender).OnDropDownChanged(e);
        }

        void OnDropDownChanged(DependencyPropertyChangedEventArgs e)
        {
            if (DropDown != null)
            {
                DropDown.PlacementTarget = this;
                DropDown.Placement = PlacementMode.Relative;
                

                this.Checked += new RoutedEventHandler((a, b) => { DropDown.IsOpen = true; });
                this.Unchecked += new RoutedEventHandler((a, b) => { DropDown.IsOpen = false; });
                DropDown.Closed += new RoutedEventHandler((a, b) => { this.IsChecked = false; });
            }
        }

        public DropDownButton()
        {
            // Bind the ToogleButton.IsChecked property to the drop-down's IsOpen property 

            Binding binding = new Binding("DropDown.IsOpen");
            binding.Source = this;
            this.SetBinding(IsCheckedProperty, binding);
        }



        // *** Overridden Methods *** 

        protected override void OnClick()
        {
            if (DropDown != null)
            {
                // If there is a drop-down assigned to this button, then position and display it 

                DropDown.PlacementTarget = this;
                DropDown.Placement = PlacementMode.Bottom;

                DropDown.IsOpen = true;
            }
        }

        #endregion
    }
}
