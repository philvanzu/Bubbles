using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bubbles4.ViewModels;

public partial class LibraryListItem:ObservableObject
{
    public string Name { get; set; }
    public MainViewModel MainViewModel { get; set; }
    
    [RelayCommand]
    public void OnSelected()
    {
        MainViewModel.OnOpenLibraryPressed(Name);
    }
    
    [RelayCommand]
    public void OnDeleted()
    {
        Task.Run(async ()=> await MainViewModel.OnDeleteLibraryPressed(Name));
    }
}