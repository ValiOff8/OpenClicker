using OpenClicker.Abstractions;
using OpenClicker.Models;

namespace OpenClicker.Services;

internal static class ProcessFilterService
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<int, ProcessInstanceId> SelectedProcesses = new();
    private static IProcessEnumerator? _processEnumerator;
    private static CancellationTokenSource? _pruneCancellation;
    private static Task? _pruneTask;

    public static event Action<ProcessInstanceId>? SelectionExpired;

    public static void Initialize(IProcessEnumerator enumerator)
    {
        ArgumentNullException.ThrowIfNull(enumerator);

        lock (SyncRoot)
        {
            if (_pruneTask is not null)
                throw new InvalidOperationException("Process filtering is already initialized.");

            SelectedProcesses.Clear();
            _processEnumerator = enumerator;
            _pruneCancellation = new CancellationTokenSource();
            _pruneTask = RunPruneLoop(_pruneCancellation.Token);
        }
    }

    public static bool TryAddProcess(ProcessInstanceId instanceId)
    {
        if (ProcessInstanceResolver.TryResolve(instanceId.ProcessId) != instanceId)
            return false;

        lock (SyncRoot)
            SelectedProcesses[instanceId.ProcessId] = instanceId;

        return true;
    }

    public static void RemoveProcess(ProcessInstanceId instanceId)
    {
        lock (SyncRoot)
        {
            if (SelectedProcesses.TryGetValue(instanceId.ProcessId, out ProcessInstanceId selected)
                && selected == instanceId)
            {
                SelectedProcesses.Remove(instanceId.ProcessId);
            }
        }
    }

    public static HashSet<ProcessInstanceId> GetSelectedProcessInstances()
    {
        lock (SyncRoot)
            return SelectedProcesses.Values.ToHashSet();
    }

    public static bool IsClickAllowed()
    {
        IProcessEnumerator? processEnumerator;

        lock (SyncRoot)
        {
            if (SelectedProcesses.Count == 0)
                return true;

            processEnumerator = _processEnumerator;
        }

        if (processEnumerator is null)
            return false;

        ProcessInstanceId? foregroundProcess = processEnumerator.GetForegroundProcess();
        if (foregroundProcess is null)
            return false;

        lock (SyncRoot)
            return SelectedProcesses.TryGetValue(foregroundProcess.Value.ProcessId, out ProcessInstanceId selected)
                && selected == foregroundProcess.Value;
    }

    private static void PruneExitedSelections()
    {
        ProcessInstanceId[] selectedInstances;
        lock (SyncRoot)
            selectedInstances = SelectedProcesses.Values.ToArray();

        foreach (ProcessInstanceId selectedInstance in selectedInstances)
        {
            if (ProcessInstanceResolver.TryResolve(selectedInstance.ProcessId) != selectedInstance)
                ExpireSelection(selectedInstance);
        }
    }

    public static async Task ShutdownAsync()
    {
        CancellationTokenSource? cancellation;
        Task? pruneTask;

        lock (SyncRoot)
        {
            cancellation = _pruneCancellation;
            pruneTask = _pruneTask;
            _pruneCancellation = null;
            _pruneTask = null;
        }

        cancellation?.Cancel();

        if (pruneTask is not null)
        {
            try
            {
                await pruneTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        cancellation?.Dispose();

        lock (SyncRoot)
        {
            SelectedProcesses.Clear();
            _processEnumerator = null;
        }
    }

    private static async Task RunPruneLoop(CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                PruneExitedSelections();
            }
            catch
            {
                // A failed prune must not disable future process-expiration checks.
            }
        }
    }

    private static void ExpireSelection(ProcessInstanceId instanceId)
    {
        bool removed;

        lock (SyncRoot)
        {
            removed = SelectedProcesses.TryGetValue(instanceId.ProcessId, out ProcessInstanceId selected)
                && selected == instanceId
                && SelectedProcesses.Remove(instanceId.ProcessId);
        }

        if (!removed || SelectionExpired is not { } handlers)
            return;

        foreach (Action<ProcessInstanceId> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(instanceId);
            }
            catch
            {
                // Selection expiration is authoritative even if a UI notification fails.
            }
        }
    }

}
