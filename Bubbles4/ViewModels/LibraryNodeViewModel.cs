using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Bubbles4.Services;

namespace Bubbles4.ViewModels;

public class LibraryNodeViewModel : LibraryViewModel
{
    public string Name { get; set; } = "";
    public bool IsAlbum { get; set; }

    public MainViewModel MainVM => _mainViewModel;
    public LibraryNodeViewModel? Parent { get; set; }

    private ObservableCollection<LibraryNodeViewModel> _children = new();
    public ObservableCollection<LibraryNodeViewModel> Children
    {
        get => _children;
        set => SetField(ref _children, value);
    }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetField(ref _isExpanded, value) && value && !IsLoaded)
            {
            }
        }
    }

    private bool _isLoaded;
    public bool IsLoaded // not sure this is useful as we're parsing the whole tree at once.
    {
        get => _isLoaded;
        set => SetField(ref _isLoaded, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public LibraryNodeViewModel(MainViewModel mainViewModel, string path)
    : base(mainViewModel, path)
    {
        
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        return true;
    }

    // Simulate loading children from disk or another source
    public void AddChild(LibraryNodeViewModel child)
    {
        // Clear dummy children
        Children.Add(child);
    }
    
    

    // Dummy child for lazy expansion
    public static LibraryNodeViewModel Dummy => new(null, "Dummy") { Name = "(Loading...)", IsAlbum = false };
}
