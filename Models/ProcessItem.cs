namespace OpenClicker.Models;

internal class ProcessItem
{
    public ProcessInstanceId InstanceId { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public string WindowTitle { get; init; } = string.Empty;
}
