using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SDL2;
using ZstdSharp.Unsafe;

namespace Bubbles4.Services;

public class SdlInputService
{
    public event EventHandler<StickEventArgs>? StickUpdated;
    public event EventHandler<ButtonEventArgs>? ButtonChanged;
    private readonly Dictionary<SDL.SDL_GameControllerButton, bool> _buttonStates = new();
    private readonly Dictionary<SDL.SDL_GameControllerAxis, double> _axisStates = new();

    public void Initialize()
    {
        if (SDL.SDL_Init(SDL.SDL_INIT_GAMECONTROLLER) != 0)
        {
            throw new Exception($"SDL_Init Error: {SDL.SDL_GetError()}");
        }
    }

    public void Shutdown()
    {
        SDL.SDL_Quit();
    }
    
    public IntPtr Controller { get; private set; } = IntPtr.Zero;
    private int _firstControllerIndex = -1; 
    public bool OpenFirstController()
    {
        int numJoysticks = SDL.SDL_NumJoysticks();
        for (int i = 0; i < numJoysticks; i++)
        {
            if (SDL.SDL_IsGameController(i) == SDL.SDL_bool.SDL_TRUE)
            {
                // it's a game controller
                Controller = SDL.SDL_GameControllerOpen(i);
                if (Controller != IntPtr.Zero)
                {
                    _firstControllerIndex = i;
                    return true;
                }
                    
            }
        }
        return false;
    }
    
    public async Task StartPollingAsync(CancellationToken token)
    {
        const int activeInterval = 8; //120 hz polling
        const int idleInterval = 1000; // 1 hz polling
        
        bool open = OpenFirstController();

        do
        {
            try
            {
                open = PollInput(); // Call your existing method
                var pollingInterval = open ? activeInterval : idleInterval;
                await Task.Delay(pollingInterval, token);     
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Controller Polling Task was Canceled.");
            }
            catch (Exception e)
            {
                Console.WriteLine(
                    $"A problem occured with the SDL2 support library, Controller support will be disabled : {e}");
                break;
            }

        } while (!token.IsCancellationRequested);
    }
    
    public bool PollInput()
    {
        SDL.SDL_Event e;
        while (SDL.SDL_PollEvent(out e) != 0)
        {
            switch (e.type)
            {
                case SDL.SDL_EventType.SDL_CONTROLLERDEVICEREMOVED:
                    Console.WriteLine("Controller removed");
                    if (Controller != IntPtr.Zero)
                    {
                        SDL.SDL_GameControllerClose(Controller);
                        Controller = IntPtr.Zero;
                    }
                    return false;

                case SDL.SDL_EventType.SDL_CONTROLLERDEVICEADDED:
                    Console.WriteLine("Controller added");
                    if (Controller == IntPtr.Zero)
                    {
                        OpenFirstController(); // Reopen controller if we weren't connected
                    }
                    break;

                default:
                    break; // Ignore other events
            }
        }

        
        if (Controller == IntPtr.Zero)
        {
            //Console.WriteLine("Controller intPtr is zero");    
            return false;
        }
            

        // Buttons to track
        var buttonsToTrack = new[]
        {
            (SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSHOULDER, ButtonName.LB),
            (SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSHOULDER, ButtonName.RB)
        };

        foreach (var button in buttonsToTrack)
        {
            bool pressed = SDL.SDL_GameControllerGetButton(Controller, button.Item1) != 0;
            if (!_buttonStates.TryGetValue(button.Item1, out var prev) || prev != pressed)
            {
                _buttonStates[button.Item1] = pressed;
                ButtonChanged?.Invoke(this, new ButtonEventArgs(button.Item2, pressed));
            }
        }
        var (x, deltax) = PollAxisGetValueAndDelta(SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX);
        var (y, deltay)  = PollAxisGetValueAndDelta(SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTY);
        if (IsOutsideDeadZone(x, y))
            StickUpdated?.Invoke(this, new StickEventArgs(StickName.LStick, x, y, deltax, deltay));

        (x, deltax) = PollAxisGetValueAndDelta(SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTX);
        (y, deltay)  = PollAxisGetValueAndDelta(SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTY);
        if (IsOutsideDeadZone(x, y))
            StickUpdated?.Invoke(this, new StickEventArgs(StickName.RStick, x, y, deltax, deltay));

        return true;
    }

    (double, double) PollAxisGetValueAndDelta(SDL.SDL_GameControllerAxis axis)
    {
        short rawValue = SDL.SDL_GameControllerGetAxis(Controller, axis);
        double normalized = rawValue / 32768.0;

        double delta = (_axisStates.TryGetValue(axis, out var prevVal))? normalized - prevVal : 0;
        _axisStates[axis] = normalized;
        return (normalized, delta);
    } 
    
    private static bool IsOutsideDeadZone(double x, double y, double threshold = 0.15)
    {
        return Math.Abs(x) > threshold || Math.Abs(y) > threshold;
    }

}


public enum StickName{LStick, RStick}
public enum AxisName {LTrigger, RTrigger}
public enum ButtonName{LB, RB, A, B, X, Y, Left, Right, Up, Down, LStick, RStick, Menu, Select}
public class StickEventArgs : EventArgs
{
    public StickName Stick { get; }
    public double X { get; }
    public double Y { get; }
    public double DeltaX { get; }
    public double DeltaY { get; }

    public StickEventArgs(StickName stick, double x, double y, double deltaX, double deltaY)
    {
        Stick = stick;
        X = x;
        Y = y;
        DeltaX = deltaX;
        DeltaY = deltaY;
    }
}

public class ButtonEventArgs : EventArgs
{
    public ButtonName Button { get; }
    public bool Pressed { get; }

    public ButtonEventArgs(ButtonName button, bool pressed)
    {
        Button = button;
        Pressed = pressed;
    }
}