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

    public static void UpdateSettings(int cps, int dutyPercent, MouseButton button)
    {
        _cps = cps;
        _dutyPercent = dutyPercent;
        _mouse_button = button;
    }

    public static Task Toggle(PhotinoWindow window)
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

                    await MouseClicker.Down(_mouse_button);
                    await Task.Delay(Math.Max(0, downMs));

                    await MouseClicker.Up(_mouse_button);
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
