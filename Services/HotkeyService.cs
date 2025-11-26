using SharpHook;
using SharpHook.Data;

namespace OpenClicker.Services;

internal static class HotkeyService
{
    public static bool IsModifier(KeyCode key) =>
        key is KeyCode.VcLeftShift or KeyCode.VcRightShift
         or KeyCode.VcLeftControl or KeyCode.VcRightControl
         or KeyCode.VcLeftAlt or KeyCode.VcRightAlt
         or KeyCode.VcLeftMeta or KeyCode.VcRightMeta;

    public static string HumanizeHotkey(Hotkey hk)
    {
        string Mod(string s) => s switch
        {
            "VcLeftControl" or "VcRightControl" => "Ctrl",
            "VcLeftAlt" or "VcRightAlt" => OperatingSystem.IsMacOS() ? "Option" : "Alt",
            "VcLeftShift" or "VcRightShift" => "Shift",
            "VcLeftMeta" or "VcRightMeta" => OperatingSystem.IsMacOS() ? "Cmd" : "Win",
            _ => s
        };

        var mods = new List<string>();
        foreach (var m in hk.Modifiers)
            mods.Add(Mod(m.ToString()));

        string main = hk.IsMouse
            ? hk.Mouse!.Value switch
            {
                MouseButton.Button4 => "Mouse4 (XButton1)",
                MouseButton.Button5 => "Mouse5 (XButton2)",
                MouseButton.Button3 => "Middle",
                MouseButton.Button1 => "Left",
                MouseButton.Button2 => "Right",
                _ => hk.Mouse!.Value.ToString()
            }
            : hk.Key!.Value.ToString().Replace("Vc", "");

        return (mods.Count > 0 ? string.Join("+", mods) + "+" : "") + main;
    }

    public static string EscapeForJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
