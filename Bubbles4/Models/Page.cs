using System;
using System.IO;

namespace Bubbles4.Models;

public class Page
{
    public required string Path { get; set; }
    public required string Name { get; set; }
    public required DateTime Created { get; set; }
    public required DateTime LastModified { get; set; }
    
    public int Index { get; set; }

    public void Update(FileInfo info)
    {
        Path = info.FullName;
        Name = info.Name;
        Created = info.CreationTime;
        LastModified = info.LastWriteTime;
    }
}

