using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using OpenClicker.Abstractions;
using OpenClicker.Models;

namespace OpenClicker.Platform.Windows;

internal class WindowsProcessEnumerator : IProcessEnumerator
{
    private static readonly HashSet<string> ExcludedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "csrss", "dwm", "lsass", "services", "svchost", "smss", "winlogon", "wininit",
        "SearchHost", "ShellExperienceHost", "StartMenuExperienceHost", "TextInputHost",
        "RuntimeBroker", "backgroundTaskHost", "dllhost", "conhost", "fontdrvhost",
        "SecurityHealthSystray", "SecurityHealthService", "sihost", "taskhostw",
        "spoolsv", "ctfmon", "SystemSettings", "ApplicationFrameHost",
        "LockApp", "LogiOverlay", "CompPkgSrv", "MsMpEng", "NisSrv",
        "SearchIndexer", "WmiPrvSE", "audiodg", "WidgetService",
        "Widgets", "PhoneExperienceHost", "UserOOBEBroker",
        "explorer", "SystemInformer", "Registry", "Idle",
        "Memory Compression", "System", "svchost", "SearchUI",
        "ShellHost", "WindowsTerminal", "OpenConsole"
    };

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

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

    public List<ProcessItem> GetVisibleWindowProcesses()
    {
        Dictionary<int, ProcessItem> processes = new();

        EnumWindows((IntPtr hWnd, IntPtr lParam) =>
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

            StringBuilder sb = new(256);
            GetWindowText(hWnd, sb, 256);
            string windowTitle = sb.ToString();

            if (string.IsNullOrWhiteSpace(windowTitle))
                return true;

            GetWindowThreadProcessId(hWnd, out uint pid);
            int processId = (int)pid;

            if (processes.ContainsKey(processId))
                return true;

            try
            {
                Process process = Process.GetProcessById(processId);
                string processName = process.ProcessName;

                if (ExcludedProcessNames.Contains(processName))
                    return true;

                processes[processId] = new ProcessItem
                {
                    ProcessId = processId,
                    ProcessName = processName,
                    WindowTitle = windowTitle
                };
            }
            catch
            {
            }

            return true;
        }, IntPtr.Zero);

        return processes.Values.OrderBy(p => p.ProcessName).ThenBy(p => p.WindowTitle).ToList();
    }

    public int? GetWindowProcessId()
    {
        IntPtr hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero)
            return null;

        GetWindowThreadProcessId(hWnd, out uint pid);
        return (int)pid;
    }
}
