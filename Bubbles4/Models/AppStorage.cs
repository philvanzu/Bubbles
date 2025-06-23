using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace Bubbles4.Models;

public class AppStorage
{
    private Dictionary<string, string> _data;

    public IReadOnlyDictionary<string, string> Data => _data;
    
    private const string RegistryPath = @"Software\Bubbles4\AppStorage";
    private const string RegistryValueName = "Storage";
    private static readonly string LinuxFilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bubbles4", "storage.json");

    private AppStorage(Dictionary<string, string> data)
    {
        this._data = data;
    }

    public void AddOrUpdate(string libraryPath, string libraryConfig)
    {
        if(_data.ContainsKey(libraryPath))_data[libraryPath] = libraryConfig;
        else _data.Add(libraryPath, libraryConfig);
    }

    public void Remove(string libraryPath)
    {
        _data.Remove(libraryPath);
    }

    public void Save()
    {
        string json = JsonSerializer.Serialize(_data);
        //save to platform appropriate app storage
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            key?.SetValue(RegistryValueName, json);
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LinuxFilePath)!);
            File.WriteAllText(LinuxFilePath, json);
        }
    }

    public static AppStorage Load()
    {
        try
        {
            string? json = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
                json = key?.GetValue(RegistryValueName) as string;
            }
            else
            {
                if (File.Exists(LinuxFilePath))
                    json = File.ReadAllText(LinuxFilePath);
            }

            if (!string.IsNullOrWhiteSpace(json))
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (data != null)
                    return new AppStorage(data);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"AppStorage load failed: {ex.Message}");
        }

        return new AppStorage(new Dictionary<string, string>());
    }

    
}