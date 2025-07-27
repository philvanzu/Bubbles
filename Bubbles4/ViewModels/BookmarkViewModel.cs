using Bubbles4.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bubbles4.ViewModels;

public partial class BookmarkViewModel: ViewModelBase
{
    private LibraryViewModel _library;
    public Bookmark Model { get; init; }
    public string Name => _library.GetBook(Model.BookPath)?.Name??Model.BookPath;
    
    public BookmarkViewModel(Bookmark model, LibraryViewModel library)
    {
        Model = model;
        _library = library;
    }
    [RelayCommand]
    private void DeleteBookmark()
    {
        _library.Config.RemoveBookmark(Model.BookPath);
    }

}