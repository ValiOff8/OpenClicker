using OpenClicker.Helpers;
using Photino.NET;
using SharpHook.Data;

namespace OpenClicker.Services;

internal static class AutoClickerService
{
    private static volatile bool _autoClickEnabled = false;
    private static int _cps = 10;
    private static int _dutyPercent = 50;
    private static MouseButton _mouse_button = MouseButton.Button1;

    private static volatile bool _holdToActivate = false;

    public static void UpdateSettings(int cps, int dutyPercent, MouseButton button)
    {
        _cps = cps;
        _dutyPercent = dutyPercent;
        _mouse_button = button;
    }

    public static bool IsRunning => _autoClickEnabled;

    public static bool HoldToActivate => _holdToActivate;

    public static void SetHoldMode(bool hold) => _holdToActivate = hold;

    public static Task Toggle(PhotinoWindow window)
    {
        if (_autoClickEnabled)
            return Stop(window);
        else
            return Start(window);
    }

    public static Task Start(PhotinoWindow window)
    {
        if (_autoClickEnabled)
            return Task.CompletedTask;

        _autoClickEnabled = true;
        window.SendWebMessage("{\"type\":\"status\",\"state\":1}");

        _ = Task.Run(async () =>
        {
            while (_autoClickEnabled)
            {
                var cps = Math.Max(1, _cps);
                var periodMs = 1000.0 / cps;

                var duty = Math.Clamp(_dutyPercent, 0, 100) / 100.0; // 0..1
                var downMs = (int)Math.Round(periodMs * duty);
                var upMs = (int)Math.Round(periodMs - downMs);

                await MouseClicker.Down(_mouse_button);
                await Task.Delay(Math.Max(0, downMs));

                await MouseClicker.Up(_mouse_button);
                await Task.Delay(Math.Max(0, upMs));
            }
        });

        return Task.CompletedTask;
    }

    public static Task Stop(PhotinoWindow window)
    {
        if (!_autoClickEnabled)
        {
            window.SendWebMessage("{\"type\":\"status\",\"state\":0}");
            return Task.CompletedTask;
        }

        _autoClickEnabled = false;
        window.SendWebMessage("{\"type\":\"status\",\"state\":0}");
        return Task.CompletedTask;
    }
}
