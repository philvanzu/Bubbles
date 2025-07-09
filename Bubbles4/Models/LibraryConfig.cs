namespace Bubbles4.Models;
using System.Text.Json;
public class LibraryConfig
{
    public string Path { get; set; }
    public enum FitTypes {Best, Width, Height, Stock }
    public enum ScrollActions { TurnPage, Scroll }
    public enum LookAndFeels {Viewer, Reader };
    //library view params
    public bool Recursive { get; set; } = true; 
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
    public int ShowPagingInfo { get; set; } // 0 : persistent // -1 : don't show // >0 : show for x seconds
    public int ShowAlbumPath { get; set; } = 5;
    public int ShowPageName { get; set; } = -1;
    public int ShowImageSize { get; set; } = -1;
    
    public enum NodeSortOptions {Alpha, Created, Modified}
    public enum SortOptions { Path, Natural, Alpha, Created, Modified, Random }
    public enum SortDirection { Ascending, Descending }
    //library sort
    public SortOptions LibrarySortOption { get; set; } = SortOptions.Natural;
    public bool LibrarySortAscending { get; set; } = true;

    //Book Sort
    public SortOptions BookSortOption { get; set; } = SortOptions.Natural;
    public bool BookSortAscending { get; set; } =true;
    
    //Node Sort
    public NodeSortOptions NodeSortOption = NodeSortOptions.Alpha;
    public bool NodeSortAscending { get; set; } = true;
    public bool ShowNavPane => !Recursive;

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