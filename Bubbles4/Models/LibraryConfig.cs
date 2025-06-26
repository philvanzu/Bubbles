using System;

namespace Bubbles4.Models;
using System;
using System.Text.Json;
public class LibraryConfig
{
    public string Path { get; set; }
    public enum FitTypes {Best, Width, Height, Stock }
    public enum ScrollActions { TurnPage, Scroll }
    public enum LookAndFeels {Viewer, Reader };
    //library view params
    public bool Recursive { get; set; } 
    public LookAndFeels LookAndFeel { get; set; } = LookAndFeels.Viewer;
    //Page viewer params
    public FitTypes Fit {
        get
        {
            if (LookAndFeel == LookAndFeels.Viewer) return FitTypes.Best;
            else return FitTypes.Width;
        }
    }

    public ScrollActions ScrollAction
    {
        get
        {
            if (LookAndFeel == LookAndFeels.Viewer) return ScrollActions.TurnPage;
            else return ScrollActions.Scroll;
        }
    } 
    public bool UseIVPs { get; set; } = true;
    public bool AnimateIVPs { get; set; } = true;
    public int ShowPagingInfo { get; set; } = 0;// 0 : persistent // -1 : don't show // >0 : show for x seconds
    public int ShowAlbumPath { get; set; } = 5;
    public int ShowPageName { get; set; } = -1;
    public int ShowImageSize { get; set; } = -1;
    
    public enum SortOptions { Path, Natural, Alpha, Created, Modified, Random }
    public enum SortDirection { Ascending, Descending }
    //library sort
    public SortOptions LibrarySortOption { get; set; } = SortOptions.Natural;
    public SortDirection LibrarySortDirection { get; set; } = SortDirection.Ascending;
    //Book Sort
    public SortOptions BookSortOption { get; set; } = SortOptions.Natural;
    public SortDirection BookSortDirection { get; set; } = SortDirection.Ascending;
    
    public bool ShowNavPane { get; set; } = false;

    public LibraryConfig(string path)
    {
        Path = path;
    }
    public string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }
    
    public static LibraryConfig? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<LibraryConfig>(json);
    }
}