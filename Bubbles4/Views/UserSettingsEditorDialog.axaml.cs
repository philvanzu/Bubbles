using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Bubbles4.Services;
using Bubbles4.ViewModels;

namespace Bubbles4.Views;

public partial class UserSettingsEditorDialog : WindowBase
{
    public UserSettingsEditorDialog() : this(null) { }
    public UserSettingsEditorDialog(UserSettingsEditorViewModel? vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm?.Initialize(this);
        Tab1Toggle.IsCheckedChanged += TabButtonToggled;
        Tab2Toggle.IsCheckedChanged += TabButtonToggled;

        

        InputManager.Instance.FocusDump = FocusDump;
        KeyUp += InputManager.Instance.OnUserSettingsEditorKeyUp;

        
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        SetTabVisible(TabPage1, TabPage2);
    }

    void SetTabVisible(Control show, Control hide)
    {
        TabParent.Children.Remove(show);
        TabParent.Children.Remove(hide);

        // Add the one you want on bottom first, then the one you want on top
        TabParent.Children.Add(hide);
        TabParent.Children.Add(show);
    }
    public void TabButtonToggled(object? sender, RoutedEventArgs e)
    {
        if (sender == null) return;
        ToggleButton Sender, Receiver;
        ContentControl SenderPage, ReceiverPage;
        
        bool pageone = sender.Equals(Tab1Toggle);
        
        Sender = pageone? Tab1Toggle : Tab2Toggle;
        SenderPage = pageone?TabPage1 : TabPage2;
        Receiver = pageone? Tab2Toggle: Tab1Toggle;
        ReceiverPage = pageone? TabPage2: TabPage1;
        
        var check = Sender.IsChecked;
        if(check.HasValue){
            Receiver.IsChecked = !check.Value;
            SenderPage.IsVisible = check.Value;
            ReceiverPage.IsVisible = !check.Value;
            SetTabVisible(check.Value ? SenderPage:ReceiverPage, check.Value?ReceiverPage:SenderPage);
        }
    }
}