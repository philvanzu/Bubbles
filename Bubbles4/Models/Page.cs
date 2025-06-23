using System;
using System.Threading;

namespace Bubbles4.Models;

public class Page
{
    public required string Path { get; init; }
    public required string Name { get; init; }
    public required DateTime Created { get; init; }
    public required DateTime LastModified { get; init; }
    
    public int Index { get; set; }
    
    public bool IsCoverPage { get; set; }
    
}

