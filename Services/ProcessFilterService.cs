using OpenClicker.Abstractions;

namespace OpenClicker.Services;

internal static class ProcessFilterService
{
    private static readonly HashSet<int> _selectedProcessIds = new();
    private static IProcessEnumerator? _processEnumerator;

    public static void Initialize(IProcessEnumerator enumerator)
    {
        _processEnumerator = enumerator;
    }

    public static void AddProcess(int processId)
    {
        lock (_selectedProcessIds)
            _selectedProcessIds.Add(processId);
    }

    public static void RemoveProcess(int processId)
    {
        lock (_selectedProcessIds)
            _selectedProcessIds.Remove(processId);
    }

    public static HashSet<int> GetSelectedProcessIds()
    {
        lock (_selectedProcessIds)
            return new HashSet<int>(_selectedProcessIds);
    }

    public static bool IsClickAllowed()
    {
        lock (_selectedProcessIds)
        {
            if (_selectedProcessIds.Count == 0)
                return true;
        }

        if (_processEnumerator is null)
            return true;

        int? foregroundPid = _processEnumerator.GetWindowProcessId();
        if (foregroundPid is null)
            return false;

        lock (_selectedProcessIds)
            return _selectedProcessIds.Contains(foregroundPid.Value);
    }
}
