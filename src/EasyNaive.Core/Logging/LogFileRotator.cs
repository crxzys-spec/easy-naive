namespace EasyNaive.Core.Logging;

public static class LogFileRotator
{
    public const long DefaultMaxBytes = 5 * 1024 * 1024;
    public const int DefaultRetainedFiles = 4;

    public static void RotateIfNeeded(
        string path,
        long maxBytes = DefaultMaxBytes,
        int retainedFiles = DefaultRetainedFiles)
    {
        if (maxBytes <= 0 || retainedFiles <= 0 || !File.Exists(path))
        {
            return;
        }

        var fileInfo = new FileInfo(path);
        if (fileInfo.Length < maxBytes)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var oldest = BuildRotatedPath(path, retainedFiles);
        if (File.Exists(oldest))
        {
            File.Delete(oldest);
        }

        for (var index = retainedFiles - 1; index >= 1; index--)
        {
            var source = BuildRotatedPath(path, index);
            if (File.Exists(source))
            {
                File.Move(source, BuildRotatedPath(path, index + 1), overwrite: true);
            }
        }

        File.Move(path, BuildRotatedPath(path, 1), overwrite: true);
    }

    private static string BuildRotatedPath(string path, int index)
    {
        return $"{path}.{index}";
    }
}
