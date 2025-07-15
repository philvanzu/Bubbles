using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace Bubbles4.Controls;

public class TouchControl: ContentControl
{
    public event EventHandler<PinchEventArgs> Pinched;
    public TouchControl()
    {
        GestureRecognizers.Add(new PinchGestureRecognizer());
        AddHandler(Gestures.PinchEvent, (_, args) => Pinched?.Invoke(this,args) );
    }
}