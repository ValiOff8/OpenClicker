using OpenClicker.Services;
using Photino.NET;
using SharpHook;
using SharpHook.Data;

namespace OpenClicker;

internal class Program
{
    private static int _cps = 10;
    private static int _dutyPercent = 50;
    private static MouseButton _mouseButton = MouseButton.Button1;
    private static volatile bool _isCapturingHotkey = false;
    private static Hotkey? _currentHotkey;
    private static bool _hotkeyLatched = false;
    private static PhotinoWindow _mainWindow = null!;

    [STAThread]
    static void Main(string[] args)
    {
        InputHookService.Initialize();
        InputHookService.KeyPressed += OnGlobalKeyPressed;
        InputHookService.KeyReleased += OnGlobalKeyReleased;
        InputHookService.MousePressed += OnGlobalMousePressed;
        InputHookService.MouseReleased += OnGlobalMouseReleased;

        AutoClickerService.UpdateSettings(_cps, _dutyPercent, _mouseButton);

        _mainWindow =
            new PhotinoWindow()
                .SetTitle("Open Clicker")
                .RegisterWebMessageReceivedHandler(RouteMessageDelegate!)
                .SetUseOsDefaultSize(false)
                .SetWidth(600)
                .SetHeight(400)
                .Center()
                .Load("wwwroot/main.html");

        _mainWindow.WaitForClose();

        InputHookService.Dispose();
    }

    static async void RouteMessageDelegate(object sender, string message)
    {
        try
        {
            var window = (PhotinoWindow)sender;

            if (string.IsNullOrEmpty(message))
                return;

            if (message.StartsWith("setCps:"))
            {
                await SetCps(message);
                return;
            }
            else if (message.StartsWith("setClickDuty:"))
            {
                await SetClickDuty(message);
                return;
            }
            else if (message.StartsWith("setMouseButton:"))
            {
                await SetMouseButton(message);
                return;
            }
            else if (message == "setKeybind")
            {
                await SetKeybind(window);
                return;
            }
            else if (message.StartsWith("setHoldMode:"))
            {
                await SetHoldMode(message);
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in RouteMessageDelegate: {ex}");
        }
    }

    static private Task SetCps(string message)
    {
        if (int.TryParse(message.Substring(7), out var cpsValue))
        {
            _cps = cpsValue;
            AutoClickerService.UpdateSettings(_cps, _dutyPercent, _mouseButton);
            Console.WriteLine($"cps updated to: {cpsValue}");
        }
        return Task.CompletedTask;
    }

    static private Task SetClickDuty(string message)
    {
        if (int.TryParse(message.Substring("setClickDuty:".Length), out var duty))
        {
            _dutyPercent = Math.Clamp(duty, 0, 100);
            AutoClickerService.UpdateSettings(_cps, _dutyPercent, _mouseButton);
            Console.WriteLine($"duty updated to: {_dutyPercent}%");
        }
        return Task.CompletedTask;
    }

    static private Task SetMouseButton(string message)
    {
        var payload = message.Substring("setMouseButton:".Length);
        if (int.TryParse(payload, out var mouseButtonValue))
        {
            _mouseButton = mouseButtonValue switch
            {
                0 => MouseButton.Button1, // Left
                1 => MouseButton.Button3, // Middle
                2 => MouseButton.Button2, // Right
                _ => MouseButton.Button1, // Default Left
            };
            AutoClickerService.UpdateSettings(_cps, _dutyPercent, _mouseButton);
            Console.WriteLine($"Mousebutton updated to: {_mouseButton}");
        }
        return Task.CompletedTask;
    }

    static private async Task SetKeybind(PhotinoWindow window)
    {
        if (HotkeyCaptureService.IsCapturing)
        {
            window.SendWebMessage("{\"type\":\"keybind\",\"text\":\"Already capturing… press a combination.\"}");
            return;
        }

        window.SendWebMessage("{\"type\":\"keybind\",\"text\":\"Press key or mouse button to set keybind\"}");

        Hotkey newHotkey;
        newHotkey = await HotkeyCaptureService.CaptureAsync();

        _currentHotkey = newHotkey;
        _hotkeyLatched = false;

        var human = HotkeyService.HumanizeHotkey(newHotkey);
        window.SendWebMessage($"{{\"type\":\"keybind\",\"text\":\"Hotkey set to: {HotkeyService.EscapeForJson(human)}\"}}");
        Console.WriteLine($"Hotkey set to: {human}");
    }

    static private Task SetHoldMode(string message)
    {
        var payload = message.Substring("setHoldMode:".Length);
        if (bool.TryParse(payload, out var holdMode))
        {
            AutoClickerService.SetHoldMode(holdMode);
            Console.WriteLine($"Hold mode updated to: {holdMode}");
        }
        return Task.CompletedTask;
    }

    private static void OnGlobalKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (HotkeyCaptureService.IsCapturing)
        {
            if (!HotkeyService.IsModifier(e.Data.KeyCode))
            {
                var hk = HotkeyCaptureService.BuildKeyboardHotkeyFromPressed(InputHookService.PressedKeys);
                if (hk.Modifiers.Length > 0)
                    HotkeyCaptureService.TrySetHotkey(hk);
                else
                    _mainWindow?.SendWebMessage("{\"type\":\"keybind\",\"text\":\"Please include a modifier (Ctrl/Cmd/Alt/Shift).\"}");
            }
            return;
        }

        if (AutoClickerService.HoldToActivate)
        {
            if (_currentHotkey is not null && !_currentHotkey.Value.IsMouse)
            {
                if (!_hotkeyLatched && IsHotkeyDown(_currentHotkey.Value))
                {
                    _hotkeyLatched = true;
                    _ = AutoClickerService.Start(_mainWindow);
                }
            }
            return;
        }

        TryToggleByHotkey();
    }

    private static void OnGlobalKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        if (HotkeyCaptureService.IsCapturing)
            return;

        if (AutoClickerService.HoldToActivate)
        {
            if (_currentHotkey is not null && !_currentHotkey.Value.IsMouse)
            {
                if (_currentHotkey.Value.Key == e.Data.KeyCode && _hotkeyLatched)
                {
                    _hotkeyLatched = false;
                    _ = AutoClickerService.Stop(_mainWindow);
                }
            }
            return;
        }

        if (_currentHotkey is not null && !_currentHotkey.Value.IsMouse)
            if (_currentHotkey.Value.Key == e.Data.KeyCode)
                _hotkeyLatched = false;
    }

    private static void OnGlobalMousePressed(object? sender, MouseHookEventArgs e)
    {
        if (HotkeyCaptureService.IsCapturing)
        {
            var hk = HotkeyCaptureService.BuildMouseHotkeyFromCurrentModifiers(e.Data.Button);
            HotkeyCaptureService.TrySetHotkey(hk);
            return;
        }

        if (AutoClickerService.HoldToActivate)
        {
            if (_currentHotkey is not null && _currentHotkey.Value.IsMouse)
            {
                if (!_hotkeyLatched && IsHotkeyDown(_currentHotkey.Value))
                {
                    _hotkeyLatched = true;
                    _ = AutoClickerService.Start(_mainWindow);
                }
            }
            return;
        }

        TryToggleByHotkey();
    }

    private static void OnGlobalMouseReleased(object? sender, MouseHookEventArgs e)
    {
        if (HotkeyCaptureService.IsCapturing)
            return;

        if (AutoClickerService.HoldToActivate)
        {
            if (_currentHotkey is not null && _currentHotkey.Value.IsMouse)
            {
                if (_currentHotkey.Value.Mouse == e.Data.Button && _hotkeyLatched)
                {
                    _hotkeyLatched = false;
                    _ = AutoClickerService.Stop(_mainWindow);
                }
            }
            return;
        }

        if (_currentHotkey is not null && _currentHotkey.Value.IsMouse)
        {
            if (_currentHotkey.Value.Mouse == e.Data.Button)
                _hotkeyLatched = false;
        }
    }

    private static void TryToggleByHotkey()
    {
        if (_currentHotkey is null || _mainWindow is null)
            return;

        if (!_hotkeyLatched && IsHotkeyDown(_currentHotkey.Value))
        {
            _hotkeyLatched = true;
            _ = AutoClickerService.Toggle(_mainWindow);
        }
    }

    private static bool IsHotkeyDown(Hotkey hk)
    {
        foreach (var modifier in hk.Modifiers)
        {
            if (modifier == KeyCode.VcLeftControl || modifier == KeyCode.VcRightControl)
            {
                if (!InputHookService.IsCtrlDown())
                    return false;
            }
            else if (modifier == KeyCode.VcLeftAlt || modifier == KeyCode.VcRightAlt)
            {
                if (!InputHookService.IsAltDown())
                    return false;
            }
            else if (modifier == KeyCode.VcLeftShift || modifier == KeyCode.VcRightShift)
            {
                if (!InputHookService.IsShiftDown())
                    return false;
            }
            else if (modifier == KeyCode.VcLeftMeta || modifier == KeyCode.VcRightMeta)
            {
                if (!InputHookService.IsMetaDown())
                    return false;
            }
        }

        //TODO: add delay so you can't accidentally toggle multiple times by holding the key down too long
        if (hk.IsMouse)
            return InputHookService.IsMousePressed(hk.Mouse!.Value);
        else
            return InputHookService.IsKeyPressed(hk.Key!.Value);
    }
}
