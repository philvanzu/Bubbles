using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Bubbles4.Models;

public class ImageViewingParams
{
    public string filename { get; set; } = "";
    public double zoom { get; set; } 
    public double centerX { get; set; }
    public double centerY { get; set; }

    public ImageViewingParams()
    {

    }

    public ImageViewingParams(string filename, double zoom, double centerX, double centerY)
    {
        this.filename = filename;
        this.zoom = zoom;
        this.centerX = centerX;
        this.centerY = centerY;
    }

    public bool IsValid => zoom != 0;


}

public class IvpCollection
{
    public List<ImageViewingParams> Collection { get; set; } = new();

    public ImageViewingParams? Get(string filename)
    {
        return Collection.FirstOrDefault(x => x.filename == filename);
    }

    public void Update(ImageViewingParams ivp)
    {
        Remove(ivp.filename);
        Collection.Add(ivp);
    }

    public void Remove(string filename)
    {
        Collection.RemoveAll(x => x.filename == filename);
    }
    public static IvpCollection? Load(string? path)
    {
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<IvpCollection>(json)!;    
        }
        return null;
    }

    public void Save(string path)
    {
        using var writer = File.CreateText(path);
        string json = JsonSerializer.Serialize(this);
        File.WriteAllText(path, json);
    }
}