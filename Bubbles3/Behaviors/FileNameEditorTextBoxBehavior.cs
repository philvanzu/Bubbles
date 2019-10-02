using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interactivity;
using System.Windows.Media;

namespace Bubbles3.Behaviors
{
    public class FileNameEditorTextBoxBehavior : Behavior<TextBox>
    {
        RoutedEventHandler GotKeyboardFocusEventHandler = new RoutedEventHandler(SelectNameText);
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.AddHandler(TextBox.GotKeyboardFocusEvent, GotKeyboardFocusEventHandler, true);
        }

        protected override void OnDetaching()
        {
            AssociatedObject.RemoveHandler(TextBox.GotKeyboardFocusEvent, GotKeyboardFocusEventHandler);
            base.OnDetaching();
        }

        private static void SelectNameText(object sender, RoutedEventArgs e)
        {
            var textBox = e.OriginalSource as TextBox;
            if (textBox != null)
            {
                //textBox.SelectAll();
                string txt = textBox.Text;
                string name = Path.GetFileNameWithoutExtension(txt);
                if ((txt.Length - name.Length) < 7)
                    textBox.Select(0, name.Length);
                else textBox.SelectAll();
            }

                
        }
    }
}

