using System.Text.Json;
using OpenClicker.Models;
using SharpHook.Data;

namespace OpenClicker.Services;

internal static class SettingsService
{
    private static readonly string _settingsPath = Path.Combine("Temp", "settings.json");

    public static Settings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var settings = JsonSerializer.Deserialize<Settings>(json);
                    if (settings is not null)
                    {
                        Console.WriteLine("Settings loaded successfully");
                        return settings;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading settings: {ex.Message}");
        }

        Console.WriteLine("Using default settings");
        return new Settings();
    }

    public static void SaveSettings(Settings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            File.WriteAllText(_settingsPath, json);
            Console.WriteLine("Settings saved successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
        }
    }

    public static Hotkey? HotkeyDataToHotkey(HotkeyData? data)
    {
        if (data is null)
            return null;

        var modifiers = data.Modifiers.Select(m => (KeyCode)m).ToArray();

        if (data.IsMouse && data.MouseButton.HasValue)
        {
            return new Hotkey((MouseButton)data.MouseButton.Value, modifiers);
        }
        else if (!data.IsMouse && data.KeyCode.HasValue)
        {
            return new Hotkey((KeyCode)data.KeyCode.Value, modifiers);
        }

        return null;
    }

    public static HotkeyData? HotkeyToHotkeyData(Hotkey? hotkey)
    {
        if (hotkey is null)
            return null;

        return new HotkeyData
        {
            IsMouse = hotkey.Value.IsMouse,
            KeyCode = hotkey.Value.Key.HasValue ? (int)hotkey.Value.Key.Value : null,
            MouseButton = hotkey.Value.Mouse.HasValue ? (int)hotkey.Value.Mouse.Value : null,
            Modifiers = hotkey.Value.Modifiers.Select(m => (int)m).ToArray()
        };
    }
}
