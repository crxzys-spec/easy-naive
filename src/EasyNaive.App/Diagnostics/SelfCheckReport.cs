using System.Text;

namespace EasyNaive.App.Diagnostics;

internal sealed class SelfCheckReport
{
    public List<SelfCheckItem> Items { get; } = new();

    public bool HasFailures => Items.Any(item => item.Status == SelfCheckStatus.Failed);

    public void AddPassed(string name, string detail)
    {
        Items.Add(new SelfCheckItem
        {
            Name = name,
            Status = SelfCheckStatus.Passed,
            Detail = detail
        });
    }

    public void AddFailed(string name, string detail)
    {
        Items.Add(new SelfCheckItem
        {
            Name = name,
            Status = SelfCheckStatus.Failed,
            Detail = detail
        });
    }

    public void AddSkipped(string name, string detail)
    {
        Items.Add(new SelfCheckItem
        {
            Name = name,
            Status = SelfCheckStatus.Skipped,
            Detail = detail
        });
    }

    public string ToDisplayText()
    {
        var builder = new StringBuilder();
        builder.AppendLine(HasFailures ? "Self-check finished with failures." : "Self-check passed.");
        builder.AppendLine();

        foreach (var item in Items)
        {
            builder
                .Append(GetPrefix(item.Status))
                .Append(' ')
                .Append(item.Name)
                .Append(": ")
                .AppendLine(item.Detail);
        }

        return builder.ToString().TrimEnd();
    }

    private static string GetPrefix(SelfCheckStatus status)
    {
        return status switch
        {
            SelfCheckStatus.Passed => "[OK]",
            SelfCheckStatus.Failed => "[FAIL]",
            _ => "[SKIP]"
        };
    }
}
