namespace EasyNaive.SingBox.ClashApi;

public sealed class ClashTrafficSnapshot
{
    public long UploadTotalBytes { get; init; }

    public long DownloadTotalBytes { get; init; }

    public int ActiveConnections { get; init; }

    public ulong MemoryBytes { get; init; }
}
