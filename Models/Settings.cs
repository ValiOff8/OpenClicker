using SharpHook.Data;

namespace OpenClicker.Models;

internal class Settings
{
    public int Cps { get; set; } = 10;
    public int ClickDuty { get; set; } = 50;
    public int MouseButton { get; set; } = 0;
    public bool HoldMode { get; set; } = false;
    public string Language { get; set; } = "en";
    public HotkeyData? Hotkey { get; set; }
}

internal class HotkeyData
{
    public bool IsMouse { get; set; }
    public int? KeyCode { get; set; }
    public int? MouseButton { get; set; }
    public int[] Modifiers { get; set; } = Array.Empty<int>();
}
