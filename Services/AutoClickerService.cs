using OpenClicker.Helpers;
using Photino.NET;
using SharpHook.Data;

namespace OpenClicker.Services;

internal static class AutoClickerService
{
    private static readonly object SyncRoot = new();
    private static readonly SemaphoreSlim ClickLoopGate = new(1, 1);
    private static volatile bool _autoClickEnabled = false;
    private static Task? _clickLoopTask;
    private static int _loopGeneration;
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
        lock (SyncRoot)
        {
            if (_autoClickEnabled)
                return Task.CompletedTask;

            _autoClickEnabled = true;
            int generation = ++_loopGeneration;
            _clickLoopTask = Task.Run(() => RunClickLoop(generation));
        }

        window.SendWebMessage("{\"type\":\"status\",\"state\":1}");

        return Task.CompletedTask;
    }

    public static Task Stop(PhotinoWindow window)
    {
        lock (SyncRoot)
        {
            _autoClickEnabled = false;
            _loopGeneration++;
        }

        window.SendWebMessage("{\"type\":\"status\",\"state\":0}");
        return Task.CompletedTask;
    }

    public static async Task ShutdownAsync()
    {
        Task? clickLoopTask;

        lock (SyncRoot)
        {
            _autoClickEnabled = false;
            _loopGeneration++;
            clickLoopTask = _clickLoopTask;
        }

        if (clickLoopTask is not null)
            await clickLoopTask;

        lock (SyncRoot)
            _clickLoopTask = null;
    }

    private static async Task RunClickLoop(int generation)
    {
        await ClickLoopGate.WaitAsync();

        try
        {
            while (_autoClickEnabled && Volatile.Read(ref _loopGeneration) == generation)
            {
                if (!ProcessFilterService.IsClickAllowed())
                {
                    await Task.Delay(50);
                    continue;
                }

                int cps = Math.Max(1, _cps);
                double periodMs = 1000.0 / cps;
                double duty = Math.Clamp(_dutyPercent, 0, 100) / 100.0;
                int downMs = (int)Math.Round(periodMs * duty);
                int upMs = (int)Math.Round(periodMs - downMs);
                MouseButton button = _mouse_button;

                await MouseClicker.Down(button);
                await Task.Delay(Math.Max(0, downMs));
                await MouseClicker.Up(button);
                await Task.Delay(Math.Max(0, upMs));
            }
        }
        finally
        {
            ClickLoopGate.Release();
        }
    }
}
