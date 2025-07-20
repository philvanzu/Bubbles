using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Bubbles4.ViewModels;

namespace Bubbles4.Views;

public partial class PageView : UserControl
{
    public PageView()
    {
        InitializeComponent();
        
        ThumbnailImage.LayoutUpdated += OnThumbnailImageLayoutUpdated;
    }

    private Size _lastSize;
    private void OnThumbnailImageLayoutUpdated(object? sender, EventArgs e)
    {
        var newSize = ThumbnailImage.Bounds.Size;

        if (_lastSize != newSize)
        {
            _lastSize = newSize;
            IvpCanvas.Width = newSize.Width;
            IvpCanvas.Height = newSize.Height;
        }
    }
}