using System.Security.Cryptography;
using System.Text;

namespace EasyNaive.Platform.Windows.DataProtection;

public sealed class WindowsDataProtection : IStringProtector
{
    public const string ProtectedValuePrefix = "dpapi:v1:";

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("EasyNaive.DataProtection.v1");

    public bool IsProtected(string value)
    {
        return value.StartsWith(ProtectedValuePrefix, StringComparison.Ordinal);
    }

    public string Protect(string value)
    {
        if (string.IsNullOrEmpty(value) || IsProtected(value))
        {
            return value;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
        return ProtectedValuePrefix + Convert.ToBase64String(protectedBytes);
    }

    public string Unprotect(string value)
    {
        if (string.IsNullOrEmpty(value) || !IsProtected(value))
        {
            return value;
        }

        var payload = value[ProtectedValuePrefix.Length..];
        var protectedBytes = Convert.FromBase64String(payload);
        var bytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}
