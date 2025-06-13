using System;

namespace Bubbles4.Models;
using System;
using System.Text.Json;
public class LibraryConfig
{
    //viewer params
    public enum FitType {Best, Width, Height }
    public FitType Fit { get; set; } = FitType.Best;
    public enum ScrollActions { TurnPage, Scroll }
    public ScrollActions ScrollAction { get; set; } = ScrollActions.TurnPage;

    public bool RestoreLastScrollPosition { get; set; } = false;
    public bool UseIVPs { get; set; } = true;
    public bool AnimateIVPs { get; set; } = true;
    public int ShowPagingInfo { get; set; } = -1;    // 0 : persistent // -1 : don't show // >0 : show for x seconds
    public int ShowAlbumName { get; set; } = -1;     // 0 : persistent // -1 : don't show // >0 : show for x seconds
    public int ShowPageName { get; set; } = -1;      // 0 : persistent // -1 : don't show // >0 : show for x seconds

    public string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }
    
    public static LibraryConfig? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<LibraryConfig>(json);
    }
}