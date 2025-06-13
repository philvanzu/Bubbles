using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bubbles4.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    // This command will be called when the item is attached (becomes visible)
    [RelayCommand]
    public virtual async Task OnAttach()
    {
        // Default implementation — override in derived classes or just handle here.
        await Task.CompletedTask;
    }

    // This command will be called when the item is detached (no longer visible)
    [RelayCommand]
    public virtual async Task OnDetach()
    {
        // Default implementation — override in derived classes or just handle here.
        await Task.CompletedTask;
    }

}