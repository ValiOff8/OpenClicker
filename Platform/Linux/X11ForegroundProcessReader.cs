using System.Runtime.InteropServices;

namespace OpenClicker.Platform.Linux;

internal sealed class X11ForegroundProcessReader : IDisposable
{
    private const string X11Library = "libX11.so.6";
    private const nuint XaAtom = 4;
    private const nuint XaCardinal = 6;
    private const nuint XaWindow = 33;

    private static readonly object InitializationLock = new();
    private static bool _initializationAttempted;
    private static bool _threadingInitialized;

    private readonly object _displayLock = new();
    private readonly nuint _activeWindowAtom;
    private readonly nuint _windowPidAtom;
    private IntPtr _display;

    private X11ForegroundProcessReader(IntPtr display, nuint activeWindowAtom, nuint windowPidAtom)
    {
        _display = display;
        _activeWindowAtom = activeWindowAtom;
        _windowPidAtom = windowPidAtom;
    }

    internal static bool TryCreate(
        out X11ForegroundProcessReader? reader,
        out string failureCode)
    {
        reader = null;
        failureCode = "x11-unavailable";
        IntPtr display = IntPtr.Zero;

        try
        {
            lock (InitializationLock)
            {
                if (!_initializationAttempted)
                {
                    _threadingInitialized = XInitThreads() != 0;
                    _initializationAttempted = true;
                }

                if (!_threadingInitialized)
                    return false;

                // XInitThreads must be the first Xlib call made by this provider.
                display = XOpenDisplay(null);
            }

            if (display == IntPtr.Zero)
                return false;

            nuint activeWindowAtom = 0;
            nuint windowPidAtom = 0;

            XLockDisplay(display);
            try
            {
                activeWindowAtom = XInternAtom(display, "_NET_ACTIVE_WINDOW", onlyIfExists: false);
                windowPidAtom = XInternAtom(display, "_NET_WM_PID", onlyIfExists: false);
                nuint supportedAtom = XInternAtom(display, "_NET_SUPPORTED", onlyIfExists: true);

                if (activeWindowAtom == 0
                    || windowPidAtom == 0
                    || supportedAtom == 0
                    || !SupportsAtom(display, supportedAtom, activeWindowAtom))
                {
                    failureCode = "ewmh-unavailable";
                    return false;
                }
            }
            finally
            {
                XUnlockDisplay(display);
            }

            reader = new X11ForegroundProcessReader(display, activeWindowAtom, windowPidAtom);
            display = IntPtr.Zero;
            failureCode = string.Empty;
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (display != IntPtr.Zero)
                XCloseDisplay(display);
        }
    }

    internal X11ForegroundWindow? GetForegroundWindow()
    {
        lock (_displayLock)
        {
            if (_display == IntPtr.Zero)
                return null;

            XLockDisplay(_display);
            try
            {
                if (!TryReadSingleProperty(_display, XDefaultRootWindow(_display), _activeWindowAtom, XaWindow, out nuint window)
                    || window == 0
                    || !TryReadSingleProperty(_display, window, _windowPidAtom, XaCardinal, out nuint processId)
                    || processId == 0
                    || processId > int.MaxValue)
                {
                    return null;
                }

                return new X11ForegroundWindow(window, (int)processId);
            }
            finally
            {
                XUnlockDisplay(_display);
            }
        }
    }

    public void Dispose()
    {
        lock (_displayLock)
        {
            if (_display == IntPtr.Zero)
                return;

            XCloseDisplay(_display);
            _display = IntPtr.Zero;
        }
    }

    private static bool SupportsAtom(
        IntPtr display,
        nuint supportedAtom,
        nuint requiredAtom)
    {
        IntPtr property = IntPtr.Zero;

        try
        {
            int status = XGetWindowProperty(
                display,
                XDefaultRootWindow(display),
                supportedAtom,
                0,
                4096,
                delete: false,
                XaAtom,
                out nuint actualType,
                out int actualFormat,
                out nuint itemCount,
                out _,
                out property);

            if (status != 0 || property == IntPtr.Zero || actualType != XaAtom || actualFormat != 32)
                return false;

            for (nuint index = 0; index < itemCount; index++)
            {
                if (ReadNativeUnsignedLong(property, index) == requiredAtom)
                    return true;
            }

            return false;
        }
        finally
        {
            if (property != IntPtr.Zero)
                XFree(property);
        }
    }

    private static bool TryReadSingleProperty(
        IntPtr display,
        nuint window,
        nuint propertyAtom,
        nuint expectedType,
        out nuint value)
    {
        value = 0;
        IntPtr property = IntPtr.Zero;

        try
        {
            int status = XGetWindowProperty(
                display,
                window,
                propertyAtom,
                0,
                1,
                delete: false,
                expectedType,
                out nuint actualType,
                out int actualFormat,
                out nuint itemCount,
                out _,
                out property);

            if (status != 0
                || property == IntPtr.Zero
                || actualType != expectedType
                || actualFormat != 32
                || itemCount < 1)
            {
                return false;
            }

            value = ReadNativeUnsignedLong(property, 0);
            return true;
        }
        finally
        {
            if (property != IntPtr.Zero)
                XFree(property);
        }
    }

    private static nuint ReadNativeUnsignedLong(IntPtr property, nuint index)
    {
        int offset = checked((int)index * IntPtr.Size);
        return IntPtr.Size == sizeof(long)
            ? unchecked((nuint)Marshal.ReadInt64(property, offset))
            : unchecked((nuint)(uint)Marshal.ReadInt32(property, offset));
    }

    [DllImport(X11Library)]
    private static extern int XInitThreads();

    [DllImport(X11Library)]
    private static extern IntPtr XOpenDisplay(string? displayName);

    [DllImport(X11Library)]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport(X11Library)]
    private static extern void XLockDisplay(IntPtr display);

    [DllImport(X11Library)]
    private static extern void XUnlockDisplay(IntPtr display);

    [DllImport(X11Library)]
    private static extern nuint XDefaultRootWindow(IntPtr display);

    [DllImport(X11Library, CharSet = CharSet.Ansi)]
    private static extern nuint XInternAtom(IntPtr display, string atomName, [MarshalAs(UnmanagedType.Bool)] bool onlyIfExists);

    [DllImport(X11Library)]
    private static extern int XGetWindowProperty(
        IntPtr display,
        nuint window,
        nuint property,
        nint longOffset,
        nint longLength,
        [MarshalAs(UnmanagedType.Bool)] bool delete,
        nuint requestedType,
        out nuint actualType,
        out int actualFormat,
        out nuint itemCount,
        out nuint bytesAfter,
        out IntPtr returnedProperty);

    [DllImport(X11Library)]
    private static extern int XFree(IntPtr data);
}

internal readonly record struct X11ForegroundWindow(nuint WindowId, int ProcessId);
