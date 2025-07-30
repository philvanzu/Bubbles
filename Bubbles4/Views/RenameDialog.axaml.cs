using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Bubbles4.ViewModels;

namespace Bubbles4.Views;

public partial class RenameDialog : Window
{
    public RenameDialog( RenameDialogViewModel vm )
    {
        InitializeComponent();
        DataContext = vm;
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is RenameDialogViewModel vm)
        {
            if(e.Key == Key.Escape)
                vm.CancelPressedCommand.Execute(null);
            if(e.Key == Key.Enter)
                vm.OkPressedCommand.Execute(null);
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is RenameDialogViewModel vm)
        {
            TextBox focusedTextBox = vm.ShowNewName ? NewNameTextBox : PrefixTextBox;
            focusedTextBox.Focus();
            if (vm.ShowNewName)
            {
                var text = focusedTextBox.Text;
                var nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(text);
                if (!string.IsNullOrEmpty(nameWithoutExt))
                {
                    focusedTextBox.SelectionStart = 0;
                    focusedTextBox.SelectionEnd = nameWithoutExt.Length;    
                }
            }
        }
    }
}