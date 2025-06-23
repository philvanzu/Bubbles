using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Bubbles4.Views; // <- replace with your actual Views namespace
using Bubbles4.ViewModels; // <- replace with your actual ViewModels namespace

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

    private Window CreateWindowForViewModel(object viewModel)
    {
        var vm = viewModel as LibraryConfigViewModel;
        return viewModel switch
        {
            LibraryConfigViewModel => new LibraryConfigWindow(vm),
            // Add additional view model mappings here
            _ => throw new NotImplementedException($"No view mapped for view model: {viewModel.GetType().Name}")
        };
    }
}