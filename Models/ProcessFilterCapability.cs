namespace OpenClicker.Models;

internal sealed record ProcessFilterCapability(bool IsAvailable, string Code, string Message);

internal sealed record ProcessCatalogResult(
    ProcessFilterCapability Capability,
    IReadOnlyList<ProcessItem> Processes);
