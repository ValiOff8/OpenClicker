using System.Diagnostics;
using OpenClicker.Models;

namespace OpenClicker.Services;

internal static class ProcessInstanceResolver
{
    public static ProcessInstanceId? TryResolve(int processId)
    {
        if (processId <= 0)
            return null;

        try
        {
            using Process process = Process.GetProcessById(processId);
            long startTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks;

            if (process.HasExited)
                return null;

            return new ProcessInstanceId(processId, startTimeUtcTicks);
        }
        catch
        {
            return null;
        }
    }
}
