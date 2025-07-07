using System;
using System.Collections.Generic;
using NaturalSort.Extension;
using Bubbles4.ViewModels;
namespace Bubbles4.Services;

class BookViewModelNaturalComparer(bool ascending = true) : IComparer<BookViewModel>
{
    private readonly NaturalSortComparer _naturalComparer = new(StringComparison.CurrentCultureIgnoreCase);

    public int Compare(BookViewModel? x, BookViewModel? y)
    {
        if (x == null) return y == null ? 0 : -1;
        if (y == null) return 1;
        var result =  _naturalComparer.Compare(x.Name, y.Name);
        return ascending ? result : -result;
    }
}

class PageViewModelNaturalComparer(bool ascending = true) : IComparer<PageViewModel>
{
    private readonly NaturalSortComparer _naturalComparer = new(StringComparison.CurrentCultureIgnoreCase);

    public int Compare(PageViewModel? x, PageViewModel? y)
    {
        if (x == null) return y == null ? 0 : -1;
        if (y == null) return 1;
        var result =  _naturalComparer.Compare(x.Name, y.Name);
        return ascending ? result : -result;
    }
}