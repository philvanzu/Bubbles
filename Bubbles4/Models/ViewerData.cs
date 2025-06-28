using Avalonia.Media.Imaging;
using Bubbles4.ViewModels;

namespace Bubbles4.Models;

public class ViewerData
{
    public required PageViewModel Page { get; set; }
    public required Bitmap? Image { get; set; }
}