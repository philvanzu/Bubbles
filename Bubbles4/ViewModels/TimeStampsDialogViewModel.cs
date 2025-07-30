using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bubbles4.ViewModels;

public partial class TimeStampsDialogViewModel:ViewModelBase
{
    BookViewModel _book;
    [ObservableProperty] DateTimeOffset? _created = null;
    [ObservableProperty] DateTimeOffset? _modified = null;
    [ObservableProperty] DateTime? _titleDate = null;

    public TimeStampsDialogViewModel(BookViewModel book)
    {
        _book = book;
        TitleDate = book.DateFromName;
        _created = book.Created;
        _modified = book.Modified;
    }

    [RelayCommand]
    void CreatedNow()
    {
        Created = new DateTimeOffset(DateTime.Now);
    }
    [RelayCommand]
    void ModifiedNow()
    {
        Modified = new DateTimeOffset(DateTime.Now);
    }

    [RelayCommand]
    void Swap()
    {
        (Modified, Created) = (Created, Modified);
    }

    [RelayCommand]
    void ModifiedToCreated()
    {
        Created = Modified;
    }

    [RelayCommand]
    void CreatedToModified()
    {
        Modified = Created;
    }

    [RelayCommand]
    void CreatedFromTitle()
    {
        if(TitleDate.HasValue)
            Created = new DateTimeOffset?(TitleDate.Value);
    }
    [RelayCommand]
    void ModifiedFromTitle()
    {
        if(TitleDate.HasValue)
            Modified = new DateTimeOffset?(TitleDate.Value);
    }

    [RelayCommand]
    void OkPressed()
    {
        if(Created == null || Created.Value == _book.Created) Created = null;
        if (Modified == null || Modified.Value == _book.Modified) Modified = null;
        
        DateTime? created = Created?.DateTime;
        DateTime? modified = Modified?.DateTime;
        
        (DateTime?, DateTime?)? ret = (created == null && modified == null) ? null : (created, modified); 
            
        
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
        {
            var window = app.Windows.FirstOrDefault(w => w.DataContext == this);
            window?.Close(ret); // Return the result from ShowDialog
        }
    }
    [RelayCommand]
    void CancelPressed()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
        {
            var window = app.Windows.FirstOrDefault(w => w.DataContext == this);
            window?.Close(null); // Return the result from ShowDialog
        }
    }
    
    
}