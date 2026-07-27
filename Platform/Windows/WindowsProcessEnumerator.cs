using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using OpenClicker.Abstractions;
using OpenClicker.Models;
using OpenClicker.Services;

namespace OpenClicker.Platform.Windows;

internal class WindowsProcessEnumerator : IProcessEnumerator
{
    private static readonly ProcessFilterCapability AvailableCapability =
        new(true, "available", "Process filtering is available.");
    private static readonly ProcessFilterCapability EnumerationFailedCapability =
        new(false, "enum-windows-failed", "Windows could not enumerate top-level windows.");

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetWindowTextLengthW(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const uint GW_OWNER = 4;
    private const int DWMWA_CLOAKED = 14;
    private IntPtr _lastForegroundWindow;
    private int _lastForegroundProcessId;
    private ProcessInstanceId? _lastForegroundProcess;
    private long _lastForegroundValidation;

    public ProcessFilterCapability Capability => AvailableCapability;

    public ProcessCatalogResult GetVisibleWindowProcesses()
    {
        Dictionary<int, ProcessItem> processes = new();

        bool enumerated = EnumWindows((IntPtr hWnd, IntPtr lParam) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOOLWINDOW) != 0)
                return true;

            if (GetWindow(hWnd, GW_OWNER) != IntPtr.Zero)
                return true;

            if (DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
                return true;

            int titleLength = GetWindowTextLengthW(hWnd);
            if (titleLength <= 0)
                return true;

            StringBuilder titleBuilder = new(titleLength + 1);
            if (GetWindowTextW(hWnd, titleBuilder, titleBuilder.Capacity) <= 0)
                return true;

            string windowTitle = titleBuilder.ToString();

            if (string.IsNullOrWhiteSpace(windowTitle))
                return true;

            if (GetWindowThreadProcessId(hWnd, out uint pid) == 0 || pid == 0 || pid > int.MaxValue)
                return true;

            int processId = (int)pid;

            if (processId == Environment.ProcessId || processes.ContainsKey(processId))
                return true;

            try
            {
                ProcessInstanceId? instanceId = ProcessInstanceResolver.TryResolve(processId);
                if (instanceId is null)
                    return true;

                using Process process = Process.GetProcessById(processId);
                string processName = process.ProcessName;

                processes[processId] = new ProcessItem
                {
                    InstanceId = instanceId.Value,
                    ProcessName = processName,
                    WindowTitle = windowTitle
                };
            }
            catch
            {
            }

            return true;
        }, IntPtr.Zero);

        IReadOnlyList<ProcessItem> result = enumerated
            ? processes.Values.OrderBy(p => p.ProcessName).ThenBy(p => p.WindowTitle).ToList()
            : [];

        return new ProcessCatalogResult(
            enumerated ? AvailableCapability : EnumerationFailedCapability,
            result);
    }

    public ProcessInstanceId? GetForegroundProcess()
    {
        IntPtr hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero)
            return null;

        if (GetWindowThreadProcessId(hWnd, out uint pid) == 0 || pid == 0 || pid > int.MaxValue)
            return null;

        int processId = (int)pid;
        long now = Environment.TickCount64;
        if (hWnd == _lastForegroundWindow
            && processId == _lastForegroundProcessId
            && now - _lastForegroundValidation < 250)
        {
            return _lastForegroundProcess;
        }

        ProcessInstanceId? foregroundProcess = ProcessInstanceResolver.TryResolve(processId);
        _lastForegroundWindow = hWnd;
        _lastForegroundProcessId = processId;
        _lastForegroundProcess = foregroundProcess;
        _lastForegroundValidation = now;
        return foregroundProcess;
    }

    public void Dispose()
    {
    }
}
