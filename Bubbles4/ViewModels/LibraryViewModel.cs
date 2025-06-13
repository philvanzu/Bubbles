using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Bubbles4.Models;

namespace Bubbles4.ViewModels;

public class LibraryViewModel: ViewModelBase
{
    ObservableCollection<BookViewModel> _books  = new();

    public ObservableCollection<BookViewModel> Books
    {
        get => _books;
        set
        {
            if (SetProperty(ref _books, value))
            {
                OnPropertyChanged(nameof(Count));
            }
        }
    }
    public int Count => Books.Count;
    public void Clear()
    {
        Books.Clear();
        OnPropertyChanged(nameof(Count));
    }

    public void AddBatch(List<BookBase> batch)
    {
        foreach (var book in batch)
        {
            _books.Add(new BookViewModel(book));
            OnPropertyChanged(nameof(Count));
            //Console.WriteLine(book.Name + " has been added to the library");
        }
    }

}