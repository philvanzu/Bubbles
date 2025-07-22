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
    
    const string _userSettingsKey = "__UserSettings__";
    const string _appStateKey = "__AppState__";
    private Dictionary<string, string> _data;
    
    
    
    public UserSettings UserSettings
    {
        get
        {
            string? json;
            _data.TryGetValue(_userSettingsKey, out json);
            UserSettings? pref = (!string.IsNullOrEmpty(json)) ? UserSettings.Deserialize(json) : null;
            
            return pref != null? pref : new UserSettings();
        }
        set
        {
            string json = value.Serialize();    
            _data[_userSettingsKey] = json;
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
            list.Remove(_userSettingsKey);
            list.Remove(_appStateKey);
            return list;
        }
    }
    
    public LibraryConfig? GetConfig(string libraryPath)
    {
        if (_data.TryGetValue(libraryPath, out var json))
        {
            var config =LibraryConfig.Deserialize(json);
            if (config != null && config.Path != libraryPath)
                config.Path = libraryPath;
            
            return config;
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
public class UserSettings
{
    public double MouseSensitivity { get; set; } = 0.5;
    public double ControllerStickSensitivity { get; set; } = 0.5;
    
    
    public double IvpAnimSpeed { get; set; } = 300;
    public float HideCursorTime { get; set; } = 5f;
    public double TurnPageBouncingTime { get; set; } = 500;
    public double ScrollSpeed { get; set; } = 40;
    
    public int ShowPagingInfo { get; set; } // 0 : persistent // -1 : don't show // >0 : show for x seconds
    public int ShowAlbumPath { get; set; } = 5;
    public int ShowPageName { get; set; } = -1;
    public int ShowImageSize { get; set; } = -1;
    public string InputBindings { get; set; } = "";
    public int CropResizeToMax { get; set; } = 5000;
    public string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }

    public static UserSettings? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<UserSettings>(json);
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

