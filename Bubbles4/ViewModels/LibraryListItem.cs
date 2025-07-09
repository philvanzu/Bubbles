using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bubbles4.ViewModels;

public partial class LibraryListItem:ViewModelBase
{
    public string Name { get; set; }
}