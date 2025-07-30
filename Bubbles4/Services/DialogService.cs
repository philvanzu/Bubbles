using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Bubbles4.Views; 
using Bubbles4.ViewModels;

namespace Bubbles4.Services;

public class DialogService : IDialogService
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

    public async Task<TResult?> ShowDialogAsync<TResult>(Window owner, object viewModel)
    {
        var window = CreateWindowForViewModel(viewModel);
        window.DataContext = viewModel;
        return await window.ShowDialog<TResult>(owner);
    }
    
    

    public void ShowModelessDialog(Window owner, object viewModel)
    {
        var window = CreateWindowForViewModel(viewModel);
        window.DataContext = viewModel;
        window.Show(owner);
    }
    public  Window CreateWindowForViewModel(object viewModel)
    {
        return viewModel switch
        {
            LibraryConfigViewModel vm => new LibraryConfigDialog(vm),
            OkCancelViewModel vm => new OkCancelDialog(vm), 
            UserSettingsEditorViewModel vm => new UserSettingsEditorDialog(vm),
            ProgressDialogViewModel vm => new ProgressDialog(vm),
            TimeStampsDialogViewModel vm => new TimeStampsDialog(vm),
            RenameDialogViewModel vm =>new RenameDialog(vm),
            _ => throw new NotImplementedException($"No view mapped for view model: {viewModel.GetType().Name}")
        };
    }

}