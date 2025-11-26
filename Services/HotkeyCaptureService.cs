using SharpHook;
using SharpHook.Data;

namespace OpenClicker.Services;

internal static class HotkeyCaptureService
{
    private static TaskCompletionSource<Hotkey>? _captureTcs;

    public static bool IsCapturing { get; private set; }

    public static async Task<Hotkey> CaptureAsync()
    {
        if (IsCapturing)
            throw new InvalidOperationException("Already capturing");

        IsCapturing = true;
        _captureTcs = new TaskCompletionSource<Hotkey>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            return await _captureTcs.Task;
        }
        finally
        {
            IsCapturing = false;
            _captureTcs = null;
        }
    }

    public static void TrySetHotkey(Hotkey hk)
    {
        _captureTcs?.TrySetResult(hk);
    }

    public static Hotkey BuildKeyboardHotkeyFromPressed(IEnumerable<KeyCode> pressedKeys)
    {
        KeyCode mainKey = KeyCode.VcUndefined;
        foreach (var k in pressedKeys)
        {
            if (!HotkeyService.IsModifier(k))
                mainKey = k;
        }
        var mods = CollectCurrentModifiers();
        return new Hotkey(mainKey, mods.ToArray());
    }

    public static Hotkey BuildMouseHotkeyFromCurrentModifiers(MouseButton button)
    {
        var mods = CollectCurrentModifiers();
        return new Hotkey(button, mods.ToArray());
    }

    private static List<KeyCode> CollectCurrentModifiers()
    {
        var modifier = new List<KeyCode>();
        if (InputHookService.IsCtrlDown()) modifier.Add(KeyCode.VcLeftControl);
        if (InputHookService.IsAltDown()) modifier.Add(KeyCode.VcLeftAlt);
        if (InputHookService.IsShiftDown()) modifier.Add(KeyCode.VcLeftShift);
        if (InputHookService.IsMetaDown()) modifier.Add(KeyCode.VcLeftMeta);
        return modifier;
    }
}
