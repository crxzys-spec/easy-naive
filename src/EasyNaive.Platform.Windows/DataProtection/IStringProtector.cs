namespace EasyNaive.Platform.Windows.DataProtection;

public interface IStringProtector
{
    bool IsProtected(string value);

    string Protect(string value);

    string Unprotect(string value);
}
