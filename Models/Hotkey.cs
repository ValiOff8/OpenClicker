using SharpHook.Data;

namespace OpenClicker;

internal readonly struct Hotkey
{
    public Hotkey(KeyCode key, KeyCode[] modifiers)
    {
        Key = key;
        Mouse = null;
        Modifiers = modifiers;
    }

    public Hotkey(MouseButton mouse, KeyCode[] modifiers)
    {
        Mouse = mouse;
        Key = null;
        Modifiers = modifiers;
    }

    public KeyCode? Key { get; }
    public MouseButton? Mouse { get; }
    public KeyCode[] Modifiers { get; }
    public bool IsMouse => Mouse.HasValue;
}
