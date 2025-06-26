using System;

namespace Bubbles4.Models;
using System;
using System.Text.Json;
public class LibraryConfig
{
 
    public enum FitTypes {Best, Width, Height, Stock }
    public enum ScrollActions { TurnPage, Scroll }

    //library view params
    public bool IncludeSubdirectories { get; set; } = false;

    //Page viewer params
    public FitTypes Fit { get; set; } = FitTypes.Best;
    public ScrollActions ScrollAction { get; set; } = ScrollActions.TurnPage;
    public bool UseIVPs { get; set; } = true;
    public bool AnimateIVPs { get; set; } = true;
    public int ShowPagingInfo { get; set; } = -1;// 0 : persistent // -1 : don't show // >0 : show for x seconds
    public int ShowPagingInfoFontSize { get; set; } = 16;
    public int ShowAlbumPath { get; set; } = -1;
    public int ShowAlbumPathFontSize { get; set; } = 16;
    public int ShowPageName { get; set; } = -1;
    public int ShowPageNameFontSize { get; set; } = 16; 
    public int ShowImageSize { get; set; } = -1;
    public int ShowImageSizeFontSize { get; set; } = 16;
    
    public SortPreferences SortPreferences { get; set; } = new ();
    
    public string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }
    
    public static LibraryConfig? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<LibraryConfig>(json);
    }
}