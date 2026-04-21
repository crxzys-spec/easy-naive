using EasyNaive.Core.Models;

namespace EasyNaive.App.Importing;

internal sealed class ManualImportPreview
{
    public string SourceName { get; init; } = string.Empty;

    public IReadOnlyList<ManualImportPreviewItem> Items { get; init; } = Array.Empty<ManualImportPreviewItem>();

    public int TotalCount => Items.Count;

    public int DuplicateCount => Items.Count(item => item.IsDuplicate);

    public int NewCount => TotalCount - DuplicateCount;
}

internal sealed class ManualImportPreviewItem
{
    public required NodeProfile Node { get; init; }

    public bool IsDuplicate { get; init; }

    public string DuplicateReason { get; init; } = string.Empty;
}
