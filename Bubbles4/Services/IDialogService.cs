using System.Threading.Tasks;
using Avalonia.Controls;

namespace Bubbles4.Services;

public interface IDialogService
{
    Task<string?> PickDirectoryAsync(Window owner);
    
    Task<TResult?> ShowDialogAsync<TResult>(Window owner, object viewModel);
    void ShowModelessDialog(Window owner, object viewModel);
}