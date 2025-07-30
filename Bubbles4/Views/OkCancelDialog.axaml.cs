using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Bubbles4.ViewModels;

namespace Bubbles4.Views;

public partial class OkCancelDialog : Window
{
    public Action<object?, KeyEventArgs>? KeyboardListener {get; set;}
    public OkCancelDialog() : this(null) { }
    public OkCancelDialog(OkCancelViewModel? vm)
    {
        InitializeComponent();
        this.DataContext = vm;
        KeyUp += OnGlobalKeyUp;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (KeyboardListener != null)
        {
            DlgBorder.Focusable = true;
            DlgBorder.Focus();    
        }
        else if (DataContext is OkCancelViewModel vm)
        {
            if( vm.ShowOkButton) 
                OkButton.Focus();
            else if (vm.ShowCancelButton) 
                CancelButton.Focus();
        }
            
        
        
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        
        AddHandler(InputElement.KeyUpEvent, OnGlobalKeyUp,  RoutingStrategies.Bubble, true);
    }

    private void OnGlobalKeyUp(object? sender, KeyEventArgs e)
    {
        KeyboardListener?.Invoke(sender, e);
    }


}