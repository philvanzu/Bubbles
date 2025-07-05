using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Microsoft.Win32;

namespace Bubbles4.Models;

public class AppStorage 
{
    private static readonly Lazy<AppStorage> _instance = new(() => Load());
    public static AppStorage Instance => _instance.Value;
    
    const string _preferencesKey = "__Preferences__";
    const string _appStateKey = "__AppState__";
    private Dictionary<string, string> _data;
    
    

    public Preferences Preferences
    {
        get
        {
            string? json;
            _data.TryGetValue(_preferencesKey, out json);
            Preferences? pref = (!string.IsNullOrEmpty(json)) ? Preferences.Deserialize(json) : null;
            
            return pref != null? pref : new Preferences();
        }
        set
        {
            string json = value.Serialize();    
            _data[_preferencesKey] = json;
        }
    }
    
    public AppState AppState
    {
        get
        {
            string? json;
            _data.TryGetValue(_appStateKey, out json);
            AppState? state = (!string.IsNullOrEmpty(json)) ? AppState.Deserialize(json) : null;
            
            return state != null? state : new AppState();
        }
        set
        {
            string json = value.Serialize();    
            _data[_appStateKey] = json;
        }
    }
    
    
    private const string RegistryPath = @"Software\Bubbles4\AppStorage";
    private const string RegistryValueName = "Storage";
    private static readonly string LinuxFilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/bubbles4", "storage.json");

    private AppStorage(Dictionary<string, string> data)
    {
        this._data = data;
        
    }

    public ObservableCollection<string> LibrariesList {
        get
        {
            var list = new ObservableCollection<string>(_data.Keys);
            list.Remove(_preferencesKey);
            list.Remove(_appStateKey);
            return list;
        }
    }
    
    public LibraryConfig? GetConfig(string libraryPath)
    {
        if (_data.TryGetValue(libraryPath, out var json))
        {
            return LibraryConfig.Deserialize(json);
        }    
        return null;
    }
    
    public void AddOrUpdate(string libraryPath, string libraryConfig)
    {
        libraryPath = libraryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        libraryPath += Path.DirectorySeparatorChar;
        _data[libraryPath] = libraryConfig;
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
            key.SetValue(RegistryValueName, json);
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LinuxFilePath)!);
            File.WriteAllText(LinuxFilePath, json);
        }
    }

    private static AppStorage Load()
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
public class Preferences
{
    public double MouseSensitivity { get; set; } = 0.5;
    public double ControllerStickSensitivity { get; set; } = 0.5;

    public string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }

    public static Preferences? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<Preferences>(json);
    }
}
public class AppState
{
    public PixelPoint WindowPosition { get; set; } = new PixelPoint(100, 100);
    public double WindowWidth { get; set; } = 800;
    public double WindowHeight { get; set; } = 600;
    public WindowState WindowState { get; set; } = WindowState.Normal;

    public string Serialize()
    {
        return JsonSerializer.Serialize(this, typeof(AppState));
    }

    public static AppState? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<AppState>(json);
    }
}

