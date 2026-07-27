using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using OpenClicker.Abstractions;
using OpenClicker.Models;
using OpenClicker.Services;

namespace OpenClicker.Platform.Linux;

internal sealed class LinuxX11ProcessEnumerator : IProcessEnumerator
{
    // There is no reliable cross-desktop distinction between user applications and desktop services.
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

    private static readonly ProcessFilterCapability AvailableCapability = new(
        true,
        "available",
        "Process filtering is available.");

    private readonly X11ForegroundProcessReader? _foregroundReader;
    private readonly bool _enumerationEnabled;
    private X11ForegroundWindow? _lastForegroundWindow;
    private ProcessInstanceId? _lastForegroundProcess;
    private long _lastForegroundValidation;

    public LinuxX11ProcessEnumerator()
    {
        if (IsNativeWayland(
                Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"),
                Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            Capability = Unavailable(
                "x11-unavailable",
                "Process filtering is unavailable in a native Wayland session.");
            return;
        }

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")))
        {
            Capability = Unavailable(
                "x11-unavailable",
                "Process filtering requires an X11 DISPLAY.");
            return;
        }

        if (!X11ForegroundProcessReader.TryCreate(out _foregroundReader, out string failureCode))
        {
            Capability = failureCode == "ewmh-unavailable"
                ? Unavailable(failureCode, "The X11 window manager does not provide the required EWMH properties.")
                : Unavailable("x11-unavailable", "The X11 display could not be opened.");
            return;
        }

        if (!IsCommandAvailable("wmctrl"))
        {
            Capability = Unavailable(
                "missing-wmctrl",
                "Process filtering requires wmctrl to enumerate application windows.");
            return;
        }

        Capability = AvailableCapability;
        _enumerationEnabled = true;
    }

    public ProcessFilterCapability Capability { get; private set; }

    public ProcessCatalogResult GetVisibleWindowProcesses()
    {
        if (!_enumerationEnabled)
            return new ProcessCatalogResult(Capability, []);

        CommandResult command = RunWmctrl();
        if (command.Status != CommandStatus.Success)
        {
            ProcessFilterCapability failure = command.Status == CommandStatus.Missing
                ? Unavailable("missing-wmctrl", "Process filtering requires wmctrl to enumerate application windows.")
                : Unavailable("enumeration-failed", BuildEnumerationFailureMessage(command.Error));

            return new ProcessCatalogResult(failure, []);
        }

        Dictionary<int, ProcessItem> processes = new();

        foreach (string line in command.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryParseWmctrlLine(line, out int pid, out string windowTitle)
                || pid == Environment.ProcessId
                || processes.ContainsKey(pid))
            {
                continue;
            }

            ProcessInstanceId? instanceId = ProcessInstanceResolver.TryResolve(pid);
            if (instanceId is not { } resolvedInstanceId)
                continue;

            string? processName = GetProcessName(pid);
            if (processName is null || ExcludedProcessNames.Contains(processName))
                continue;

            processes.Add(pid, new ProcessItem
            {
                InstanceId = resolvedInstanceId,
                ProcessName = processName,
                WindowTitle = windowTitle
            });
        }

        List<ProcessItem> orderedProcesses = processes.Values
            .OrderBy(process => process.ProcessName)
            .ThenBy(process => process.WindowTitle)
            .ToList();

        return new ProcessCatalogResult(Capability, orderedProcesses);
    }

    public ProcessInstanceId? GetForegroundProcess()
    {
        X11ForegroundWindow? foregroundWindow = _foregroundReader?.GetForegroundWindow();
        if (foregroundWindow is null)
            return null;

        long now = Environment.TickCount64;
        if (foregroundWindow == _lastForegroundWindow && now - _lastForegroundValidation < 250)
            return _lastForegroundProcess;

        ProcessInstanceId? foregroundProcess = ProcessInstanceResolver.TryResolve(foregroundWindow.Value.ProcessId);
        _lastForegroundWindow = foregroundWindow;
        _lastForegroundProcess = foregroundProcess;
        _lastForegroundValidation = now;
        return foregroundProcess;
    }

    public void Dispose() => _foregroundReader?.Dispose();

    private static bool IsNativeWayland(string? sessionType, string? waylandDisplay)
    {
        string? normalizedSessionType = sessionType?.Trim();

        if (string.Equals(normalizedSessionType, "wayland", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(normalizedSessionType, "x11", StringComparison.OrdinalIgnoreCase))
            return false;

        return !string.IsNullOrWhiteSpace(waylandDisplay);
    }

    private static bool TryParseWmctrlLine(string line, out int pid, out string windowTitle)
    {
        pid = 0;
        windowTitle = string.Empty;

        int position = 0;
        string? processIdText = null;

        for (int field = 0; field < 4; field++)
        {
            while (position < line.Length && char.IsWhiteSpace(line[position]))
                position++;

            int start = position;
            while (position < line.Length && !char.IsWhiteSpace(line[position]))
                position++;

            if (start == position)
                return false;

            if (field == 2)
                processIdText = line[start..position];
        }

        while (position < line.Length && char.IsWhiteSpace(line[position]))
            position++;

        if (position == line.Length
            || !int.TryParse(processIdText, NumberStyles.None, CultureInfo.InvariantCulture, out pid)
            || pid <= 0)
        {
            return false;
        }

        windowTitle = line[position..].TrimEnd('\r');
        return !string.IsNullOrWhiteSpace(windowTitle);
    }

    private static ProcessFilterCapability Unavailable(string code, string message) => new(false, code, message);

    private static string BuildEnumerationFailureMessage(string error)
    {
        const string prefix = "wmctrl could not enumerate X11 windows.";
        if (string.IsNullOrWhiteSpace(error))
            return prefix;

        string singleLineError = error.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (singleLineError.Length > 240)
            singleLineError = singleLineError[..240];

        return $"{prefix} {singleLineError}";
    }

    private static string? GetProcessName(int pid)
    {
        try
        {
            FileSystemInfo? target = File.ResolveLinkTarget($"/proc/{pid}/exe", returnFinalTarget: true);
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

    private static bool IsCommandAvailable(string command)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return false;

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(directory.Trim().Trim('"'), command);
                if (File.Exists(candidate) && IsExecutable(candidate))
                    return true;
            }
            catch
            {
            }
        }

        return false;
    }

    private static bool IsExecutable(string path)
    {
        UnixFileMode executableBits = UnixFileMode.UserExecute
            | UnixFileMode.GroupExecute
            | UnixFileMode.OtherExecute;

        return OperatingSystem.IsLinux()
            && (File.GetUnixFileMode(path) & executableBits) != 0;
    }

    private static CommandResult RunWmctrl()
    {
        using Process process = new();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "wmctrl",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.StartInfo.ArgumentList.Add("-l");
        process.StartInfo.ArgumentList.Add("-p");

        try
        {
            process.Start();
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 2)
        {
            return new CommandResult(CommandStatus.Missing, string.Empty, exception.Message);
        }
        catch (Exception exception)
        {
            return new CommandResult(CommandStatus.Failed, string.Empty, exception.Message);
        }

        try
        {
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(3000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }
                }

                process.WaitForExit(1000);
                return new CommandResult(CommandStatus.Failed, string.Empty, "wmctrl timed out after 3 seconds.");
            }

            if (!Task.WhenAll(standardOutput, standardError).Wait(1000))
                return new CommandResult(CommandStatus.Failed, string.Empty, "wmctrl output could not be read.");

            string output = standardOutput.GetAwaiter().GetResult();
            string error = standardError.GetAwaiter().GetResult();

            return process.ExitCode == 0
                ? new CommandResult(CommandStatus.Success, output, error)
                : new CommandResult(CommandStatus.Failed, output, error);
        }
        catch (Exception exception)
        {
            return new CommandResult(CommandStatus.Failed, string.Empty, exception.Message);
        }
    }

    private enum CommandStatus
    {
        Success,
        Missing,
        Failed
    }

    private readonly record struct CommandResult(CommandStatus Status, string Output, string Error);
}
