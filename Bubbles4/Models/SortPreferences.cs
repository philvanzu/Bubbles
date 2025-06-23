namespace Bubbles4.Models;

public class SortPreferences
{
    public enum SortOptions { Path, Natural, Alpha, Created, Modified, Random }
    public enum SortDirection { Ascending, Descending }
    public SortOptions LibrarySortOption { get; set; } = SortOptions.Natural;
    public SortDirection LibrarySortDirection { get; set; } = SortDirection.Ascending;
    //Book view params
    public SortOptions BookSortOption { get; set; } = SortOptions.Natural;
    public SortDirection BookSortDirection { get; set; } = SortDirection.Ascending;
}