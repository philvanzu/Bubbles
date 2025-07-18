using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Bubbles4.Controls;

public class TouchControl: ContentControl
{
    public event EventHandler<PinchEventArgs>? Pinched;
    public TouchControl()
    {
        GestureRecognizers.Add(new PinchGestureRecognizer());
        AddHandler(Gestures.PinchEvent, (_, args) => Pinched?.Invoke(this,args), RoutingStrategies.Bubble);
    }
}