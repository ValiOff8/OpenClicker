using System.Diagnostics;
using OpenClicker.Abstractions;
using OpenClicker.Models;

namespace OpenClicker.Platform.Linux;

internal class LinuxX11ProcessEnumerator : IProcessEnumerator
{
    //Note: we need this because there is no reliable way to distinguish user-relevant processes from system/background processes
    private static readonly HashSet<string> ExcludedProcessNames = new(StringComparer.Ordinal)
    {
        "gnome-shell", "plasmashell", "kwin_x11", "kwin_wayland",
        "mutter", "marco", "xfwm4", "openbox", "i3", "sway", "bspwm",
        "herbstluftwm", "awesome", "dwm", "xmonad", "hyprland",
        "polybar", "waybar", "lxpanel", "xfce4-panel", "mate-panel",
        "gnome-panel", "budgie-panel", "tint2", "plank", "cairo-dock",
        "dunst", "mako", "notify-osd", "xfce4-notifyd",
        "nm-applet", "blueman-applet", "blueman-tray", "pasystray",
        "pulseaudio", "pipewire", "wireplumber",
        "dbus-daemon", "dbus-broker", "systemd", "Xorg", "X", "Xwayland",
        "ibus-daemon", "ibus-ui-gtk3", "fcitx5", "fcitx",
        "polkit-gnome-authentication-agent-1",
        "xdg-desktop-portal", "xdg-desktop-portal-gtk", "xdg-desktop-portal-gnome",
        "gvfsd", "gvfsd-fuse", "tracker-miner-fs-3",
        "xss-lock", "light-locker", "gnome-screensaver",
        "picom", "compton", "xcompmgr",
        "at-spi-bus-launcher", "at-spi2-registryd",
        "gsd-color", "gsd-keyboard", "gsd-media-keys", "gsd-power", "gsd-wacom",
        "gnome-keyring-daemon", "ssh-agent", "gpg-agent",
        "dconf-service", "xdg-permission-store",
        "evolution-alarm-notify", "evolution-data-server",
        "goa-daemon", "goa-identity-service",
        "snapd", "snap-store", "packagekitd",
        "xfconfd", "xfsettingsd",
    };

    private readonly bool _wmctrlAvailable;
    private readonly bool _xdotoolAvailable;

    public LinuxX11ProcessEnumerator()
    {
        _wmctrlAvailable = IsCommandAvailable("wmctrl");
        _xdotoolAvailable = IsCommandAvailable("xdotool");

        if (!_wmctrlAvailable)
            Console.WriteLine("Warning: wmctrl not found. Install with: sudo apt install wmctrl");
        if (!_xdotoolAvailable)
            Console.WriteLine("Warning: xdotool not found. Install with: sudo apt install xdotool");
    }

    public List<ProcessItem> GetVisibleWindowProcesses()
    {
        if (!_wmctrlAvailable)
            return [];

        string? output = RunCommand("wmctrl", "-l -p");
        if (output is null)
            return [];

        Dictionary<int, ProcessItem> processes = new();

        foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // wmctrl -l -p format: 0x04000003  0 12345 hostname Window Title
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
                continue;

            if (!int.TryParse(parts[2], out int pid) || pid <= 0)
                continue;

            if (processes.ContainsKey(pid))
                continue;

            string windowTitle = string.Join(' ', parts.Skip(4));
            if (string.IsNullOrWhiteSpace(windowTitle))
                continue;

            string? processName = GetProcessName(pid);
            if (processName is null)
                continue;

            if (ExcludedProcessNames.Contains(processName))
                continue;

            string displayName = GetWindowClass(parts[0]) ?? processName;

            processes[pid] = new ProcessItem
            {
                ProcessId = pid,
                ProcessName = displayName,
                WindowTitle = windowTitle
            };
        }

        return processes.Values.OrderBy(p => p.ProcessName).ThenBy(p => p.WindowTitle).ToList();
    }

    public int? GetWindowProcessId()
    {
        if (!_xdotoolAvailable)
            return null;

        string? output = RunCommand("xdotool", "getactivewindow getwindowpid");
        if (output is null)
            return null;

        if (int.TryParse(output.Trim(), out int pid) && pid > 0)
            return pid;

        return null;
    }

    private static string? GetProcessName(int pid)
    {
        try
        {
            string exeLink = $"/proc/{pid}/exe";
            FileSystemInfo? target = File.ResolveLinkTarget(exeLink, returnFinalTarget: true);
            if (target is not null)
                return Path.GetFileName(target.FullName);
        }
        catch
        {
        }

        try
        {
            string commPath = $"/proc/{pid}/comm";
            if (File.Exists(commPath))
                return File.ReadAllText(commPath).Trim();
        }
        catch
        {
        }

        return null;
    }

    private static string? GetWindowClass(string windowId)
    {
        string? output = RunCommand("xprop", $"-id {windowId} WM_CLASS");
        if (output is null)
            return null;

        // xprop output: WM_CLASS(STRING) = "instance", "ClassName"
        int eqIndex = output.IndexOf('=');
        if (eqIndex < 0)
            return null;

        string[] values = output[(eqIndex + 1)..].Split(',');

        if (values.Length >= 2)
        {
            string className = values[1].Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(className))
                return className;
        }

        if (values.Length >= 1)
        {
            string instanceName = values[0].Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(instanceName))
                return instanceName;
        }

        return null;
    }

    private static bool IsCommandAvailable(string command)
    {
        string? result = RunCommand("which", command);
        return result is not null && !string.IsNullOrWhiteSpace(result);
    }

    private static string? RunCommand(string command, string arguments)
    {
        try
        {
            using Process process = new();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);

            if (process.ExitCode != 0)
                return null;

            return output;
        }
        catch
        {
            return null;
        }
    }
}
