namespace EasyNaive.App.Diagnostics;

internal sealed class SelfCheckItem
{
    public required string Name { get; init; }

    public required SelfCheckStatus Status { get; init; }

    public required string Detail { get; init; }
}
