using Photino.NET;
using SharpHook;
using SharpHook.Data;
using System.Collections.Concurrent;

namespace OpenClicker;

internal class Program
{
    private static volatile bool _autoClickEnabled = false;

    private static TaskPoolGlobalHook _globalHook = null!;
    private static readonly ConcurrentDictionary<KeyCode, byte> _pressed = new();
    private static readonly ConcurrentDictionary<MouseButton, byte> _mousePressed = new();

    private static volatile bool _isCapturingHotkey = false;
    private static TaskCompletionSource<Hotkey>? _captureTcs;

    private static Hotkey? _currentHotkey;

    private static bool _hotkeyLatched = false;

    private static PhotinoWindow _mainWindow = null!;

    private static int _cps = 10;
    private static int _dutyPercent = 50;
    private static MouseButton _mouseButton = MouseButton.Button1;

    [STAThread]
    static void Main(string[] args)
    {
        _globalHook = new TaskPoolGlobalHook();
        _globalHook.KeyPressed += OnGlobalKeyPressed;
        _globalHook.KeyReleased += OnGlobalKeyReleased;

        _globalHook.MousePressed += OnGlobalMousePressed;
        _globalHook.MouseReleased += OnGlobalMouseReleased;

        _ = _globalHook.RunAsync();

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

        _globalHook.Dispose();
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
            Console.WriteLine($"cps updated to: {cpsValue}");
        }
        return Task.CompletedTask;
    }

    static private Task SetClickDuty(string message)
    {
        if (int.TryParse(message.Substring("setClickDuty:".Length), out var duty))
        {
            _dutyPercent = Math.Clamp(duty, 0, 100);
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
            Console.WriteLine($"Mousebutton updated to: {_mouseButton}");
        }
        return Task.CompletedTask;
    }

    static private async Task SetKeybind(PhotinoWindow window)
    {
        if (_isCapturingHotkey)
        {
            window.SendWebMessage("{\"type\":\"keybind\",\"text\":\"Already capturing… press a combination.\"}");
            return;
        }

        _isCapturingHotkey = true;
        _captureTcs = new TaskCompletionSource<Hotkey>(TaskCreationOptions.RunContinuationsAsynchronously);

        window.SendWebMessage("{\"type\":\"keybind\",\"text\":\"Press key or mouse button to set keybind\"}");

        Hotkey newHotkey;
        try
        {
            newHotkey = await _captureTcs.Task;
        }
        finally
        {
            _isCapturingHotkey = false;
            _captureTcs = null;
        }

        _currentHotkey = newHotkey;
        _hotkeyLatched = false;

        var human = HumanizeHotkey(newHotkey);
        window.SendWebMessage($"{{\"type\":\"keybind\",\"text\":\"Hotkey set to: {EscapeForJson(human)}\"}}");
        Console.WriteLine($"Hotkey set to: {human}");
    }

    private static void OnGlobalKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        _pressed[e.Data.KeyCode] = 1;

        if (_isCapturingHotkey)
        {
            if (!IsModifier(e.Data.KeyCode))
            {
                var hk = BuildKeyboardHotkeyFromPressed();

                if (hk.Modifiers.Length > 0)
                    _captureTcs?.TrySetResult(hk);
                else
                    _mainWindow?.SendWebMessage("{\"type\":\"keybind\",\"text\":\"Please include a modifier (Ctrl/Cmd/Alt/Shift).\"}");
            }
            return;
        }

        TryToggleByHotkey();
    }

    private static void OnGlobalKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        _pressed.TryRemove(e.Data.KeyCode, out _);

        if (_currentHotkey is not null && !_currentHotkey.Value.IsMouse)
            if (_currentHotkey.Value.Key == e.Data.KeyCode)
                _hotkeyLatched = false;
    }

    private static void OnGlobalMousePressed(object? sender, MouseHookEventArgs e)
    {
        _mousePressed[e.Data.Button] = 1;

        if (_isCapturingHotkey)
        {
            var hk = BuildMouseHotkeyFromCurrentModifiers(e.Data.Button);
            _captureTcs?.TrySetResult(hk);
            return;
        }

        TryToggleByHotkey();
    }

    private static void OnGlobalMouseReleased(object? sender, MouseHookEventArgs e)
    {
        _mousePressed.TryRemove(e.Data.Button, out _);

        if (_currentHotkey is not null && _currentHotkey.Value.IsMouse)
        {
            if (_currentHotkey.Value.Mouse == e.Data.Button)
                _hotkeyLatched = false;
        }
    }

    private static bool IsModifier(KeyCode key) =>
        key is KeyCode.VcLeftShift or KeyCode.VcRightShift
         or KeyCode.VcLeftControl or KeyCode.VcRightControl
         or KeyCode.VcLeftAlt or KeyCode.VcRightAlt
         or KeyCode.VcLeftMeta or KeyCode.VcRightMeta;

    private static bool IsCtrlDown() => _pressed.ContainsKey(KeyCode.VcLeftControl) || _pressed.ContainsKey(KeyCode.VcRightControl);

    private static bool IsAltDown() => _pressed.ContainsKey(KeyCode.VcLeftAlt) || _pressed.ContainsKey(KeyCode.VcRightAlt);

    private static bool IsShiftDown() => _pressed.ContainsKey(KeyCode.VcLeftShift) || _pressed.ContainsKey(KeyCode.VcRightShift);

    private static bool IsMetaDown() => _pressed.ContainsKey(KeyCode.VcLeftMeta) || _pressed.ContainsKey(KeyCode.VcRightMeta);

    private static Hotkey BuildKeyboardHotkeyFromPressed()
    {
        KeyCode mainKey = KeyCode.VcUndefined;
        foreach (var kv in _pressed)
        {
            if (!IsModifier(kv.Key))
                mainKey = kv.Key;
        }

        var mods = CollectCurrentModifiers();
        return new Hotkey(mainKey, mods.ToArray());
    }

    private static Hotkey BuildMouseHotkeyFromCurrentModifiers(MouseButton button)
    {
        var mods = CollectCurrentModifiers();
        return new Hotkey(button, mods.ToArray());
    }

    private static List<KeyCode> CollectCurrentModifiers()
    {
        var modifier = new List<KeyCode>();
        if (IsCtrlDown()) modifier.Add(KeyCode.VcLeftControl);
        if (IsAltDown()) modifier.Add(KeyCode.VcLeftAlt);
        if (IsShiftDown()) modifier.Add(KeyCode.VcLeftShift);
        if (IsMetaDown()) modifier.Add(KeyCode.VcLeftMeta);
        return modifier;
    }

    private static void TryToggleByHotkey()
    {
        if (_currentHotkey is null || _mainWindow is null)
            return;

        if (!_hotkeyLatched && IsHotkeyDown(_currentHotkey.Value))
        {
            _hotkeyLatched = true;
            _ = TurnOnOffAutoClicker(_mainWindow);
        }
    }

    private static bool IsHotkeyDown(Hotkey hk)
    {
        foreach (var modifier in hk.Modifiers)
        {
            if (modifier == KeyCode.VcLeftControl || modifier == KeyCode.VcRightControl)
            {
                if (!IsCtrlDown())
                    return false;
            }
            else if (modifier == KeyCode.VcLeftAlt || modifier == KeyCode.VcRightAlt)
            {
                if (!IsAltDown())
                    return false;
            }
            else if (modifier == KeyCode.VcLeftShift || modifier == KeyCode.VcRightShift)
            {
                if (!IsShiftDown())
                    return false;
            }
            else if (modifier == KeyCode.VcLeftMeta || modifier == KeyCode.VcRightMeta)
            {
                if (!IsMetaDown())
                    return false;
            }
        }

        if (hk.IsMouse)
            return _mousePressed.ContainsKey(hk.Mouse!.Value);
        else
            return _pressed.ContainsKey(hk.Key!.Value);
    }

    private static string HumanizeHotkey(Hotkey hk)
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

    private static string EscapeForJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private readonly struct Hotkey
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

    private static Task TurnOnOffAutoClicker(PhotinoWindow window)
    {
        _autoClickEnabled = !_autoClickEnabled;

        if (_autoClickEnabled)
        {
            window.SendWebMessage("{\"type\":\"status\",\"text\":\"AutoClicker: ON\"}");

            _ = Task.Run(async () =>
            {
                while (_autoClickEnabled)
                {
                    var cps = Math.Max(1, _cps);
                    var periodMs = 1000.0 / cps;

                    var duty = Math.Clamp(_dutyPercent, 0, 100) / 100.0; // 0..1
                    var downMs = (int)Math.Round(periodMs * duty);
                    var upMs = (int)Math.Round(periodMs - downMs);

                    await MouseClicker.Down(_mouseButton);
                    await Task.Delay(Math.Max(0, downMs));

                    await MouseClicker.Up(_mouseButton);
                    await Task.Delay(Math.Max(0, upMs));
                }
            });

            return Task.CompletedTask;
        }
        else
        {
            window.SendWebMessage("{\"type\":\"status\",\"text\":\"AutoClicker: OFF\"}");
            return Task.CompletedTask;
        }
    }
}
