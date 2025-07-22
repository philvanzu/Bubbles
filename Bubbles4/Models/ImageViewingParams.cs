using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bubbles4.Models;

public class ImageViewingParams
{
    public string filename { get; set; }
    public double zoom { get; set; } 
    public double centerX { get; set; }
    public double centerY { get; set; }

    public ImageViewingParams(string filename, double zoom, double centerX, double centerY)
    {
        this.filename = filename;
        this.zoom = zoom;
        this.centerX = centerX;
        this.centerY = centerY;
    }

    public bool IsValid => zoom != 0;


}

public class BookMetadata
{
    public List<ImageViewingParams> Collection { get; set; } = new();
    [JsonIgnore] public bool IsDirty { get; private set; }

    
    
    public ImageViewingParams? Get(string filename)
    {
        return Collection.FirstOrDefault(x => x.filename == filename);
    }

    public void AddOrUpdate(ImageViewingParams ivp)
    {
        Remove(ivp.filename);
        Collection.Add(ivp);
        IsDirty = true;
    }

    public void Remove(string filename)
    {
        Collection.RemoveAll(x => x.filename == filename);
        IsDirty = true;
    }
    
    public void Save(string path)
    {
        if (IsDirty == false) return;
        IsDirty = false;
        if (Collection.Count > 0)
        {
            try
            {
                using var writer = File.CreateText(path);
                string json = JsonSerializer.Serialize(this);
                File.WriteAllText(path, json);    
            }
            catch (Exception ex){Console.Error.WriteLine(ex);}
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
    public static BookMetadata Load(string? path)
    {
        if (File.Exists(path))
        {
            try{
                var json = File.ReadAllText(path);
                var ivps = JsonSerializer.Deserialize<BookMetadata>(json);
                return ivps ?? new BookMetadata();
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"Corrupt IVP file at {path}: {ex.Message}");

                try
                {
                    File.Delete(path);
                    Console.Error.WriteLine($"Deleted corrupt IVP file: {path}");
                }
                catch (Exception deleteEx)
                {
                    Console.Error.WriteLine($"Failed to delete corrupt file: {deleteEx.Message}");
                }

                return new BookMetadata();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unexpected error loading IVP file: {ex.Message}");
                return new BookMetadata();
            }
        }
        else return new BookMetadata();
    }

}