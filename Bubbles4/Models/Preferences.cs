using System.Text.Json;

namespace Bubbles4.Models;

public class Preferences
{
    public double MouseSensitivity { get; set; } = 0.5;
    public double ControllerStickSensitivity { get; set; } = 0.5;

    public string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }

    public static Preferences? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<Preferences>(json);
    }
}