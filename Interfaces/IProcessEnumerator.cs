using OpenClicker.Models;

namespace OpenClicker.Abstractions;

internal interface IProcessEnumerator : IDisposable
{
    ProcessFilterCapability Capability { get; }
    ProcessCatalogResult GetVisibleWindowProcesses();
    ProcessInstanceId? GetForegroundProcess();
}
