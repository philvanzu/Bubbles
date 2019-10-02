using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Interactivity;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;

namespace Bubbles3.Behaviors
{
    public class ClickSelectTextBox: Behavior<TextBox>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.AddHandler(TextBox.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(SelectivelyIgnoreMouseButton), true);
            AssociatedObject.AddHandler(TextBox.GotKeyboardFocusEvent, new RoutedEventHandler(SelectAllText), true);
            AssociatedObject.AddHandler(TextBox.MouseDoubleClickEvent, new RoutedEventHandler(SelectAllText), true);
        }

        private static void SelectivelyIgnoreMouseButton(object sender,
                                                    MouseButtonEventArgs e)
        {
            // Find the TextBox
            DependencyObject parent = e.OriginalSource as UIElement;
            while (parent != null && !(parent is TextBox))
                parent = VisualTreeHelper.GetParent(parent);

            if (parent != null)
            {
                var textBox = (TextBox)parent;
                if (!textBox.IsKeyboardFocusWithin)
                {
                    // If the text box is not yet focussed, give it the focus and
                    // stop further processing of this click event.
                    textBox.Focus();
                    e.Handled = true;
                }
            }
        }

        private static void SelectAllText(object sender, RoutedEventArgs e)
        {
            var textBox = e.OriginalSource as TextBox;
            if (textBox != null)
                textBox.SelectAll();
        }
    }
}
