using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Bubbles4.Services;

public class DialogService: IDialogService
{
    public async Task<string?> PickDirectoryAsync(Window owner)
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Select a folder"
        });
        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }
    
}