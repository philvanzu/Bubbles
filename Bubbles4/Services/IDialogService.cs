using System.Threading.Tasks;
using Avalonia.Controls;

namespace Bubbles4.Services;

public interface IDialogService
{
    Task<string?> PickDirectoryAsync(Window owner);
}