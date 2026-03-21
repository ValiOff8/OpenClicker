using OpenClicker.Models;

namespace OpenClicker.Abstractions;

internal interface IProcessEnumerator
{
    List<ProcessItem> GetVisibleWindowProcesses();
    int? GetWindowProcessId();
}
