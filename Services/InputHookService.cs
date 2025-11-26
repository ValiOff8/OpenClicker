using SharpHook;
using SharpHook.Data;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace OpenClicker.Services;

internal static class InputHookService
{
    public static TaskPoolGlobalHook GlobalHook { get; private set; } = null!;

    private static readonly ConcurrentDictionary<KeyCode, byte> _pressed = new();
    private static readonly ConcurrentDictionary<MouseButton, byte> _mousePressed = new();

    public static IReadOnlyCollection<KeyCode> PressedKeys => new List<KeyCode>(_pressed.Keys);

    public static void Initialize()
    {
        GlobalHook = new TaskPoolGlobalHook();
        GlobalHook.KeyPressed += OnGlobalKeyPressed;
        GlobalHook.KeyReleased += OnGlobalKeyReleased;
        GlobalHook.MousePressed += OnGlobalMousePressed;
        GlobalHook.MouseReleased += OnGlobalMouseReleased;

        _ = GlobalHook.RunAsync();
    }

    public static void Dispose()
    {
        GlobalHook.Dispose();
    }

    public static event EventHandler<KeyboardHookEventArgs>? KeyPressed;
    public static event EventHandler<KeyboardHookEventArgs>? KeyReleased;
    public static event EventHandler<MouseHookEventArgs>? MousePressed;
    public static event EventHandler<MouseHookEventArgs>? MouseReleased;

    private static void OnGlobalKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        _pressed[e.Data.KeyCode] = 1;
        KeyPressed?.Invoke(sender, e);
    }

    private static void OnGlobalKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        _pressed.TryRemove(e.Data.KeyCode, out _);
        KeyReleased?.Invoke(sender, e);
    }

    private static void OnGlobalMousePressed(object? sender, MouseHookEventArgs e)
    {
        _mousePressed[e.Data.Button] = 1;
        MousePressed?.Invoke(sender, e);
    }

    private static void OnGlobalMouseReleased(object? sender, MouseHookEventArgs e)
    {
        _mousePressed.TryRemove(e.Data.Button, out _);
        MouseReleased?.Invoke(sender, e);
    }

    public static bool IsCtrlDown() => _pressed.ContainsKey(KeyCode.VcLeftControl) || _pressed.ContainsKey(KeyCode.VcRightControl);
    public static bool IsAltDown() => _pressed.ContainsKey(KeyCode.VcLeftAlt) || _pressed.ContainsKey(KeyCode.VcRightAlt);
    public static bool IsShiftDown() => _pressed.ContainsKey(KeyCode.VcLeftShift) || _pressed.ContainsKey(KeyCode.VcRightShift);
    public static bool IsMetaDown() => _pressed.ContainsKey(KeyCode.VcLeftMeta) || _pressed.ContainsKey(KeyCode.VcRightMeta);
    
    public static bool IsKeyPressed(KeyCode key) => _pressed.ContainsKey(key);
    public static bool IsMousePressed(MouseButton btn) => _mousePressed.ContainsKey(btn);

    //TODO: add single key bind support
    public static IEnumerable<KeyCode> GetNonModifierPressedKeys()
    {
        foreach (var k in _pressed.Keys)
        {
            if (!HotkeyService.IsModifier(k))
                yield return k;
        }
    }
}
