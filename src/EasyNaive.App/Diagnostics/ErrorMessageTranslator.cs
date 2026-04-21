using System.Net.Http;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace EasyNaive.App.Diagnostics;

internal static partial class ErrorMessageTranslator
{
    public static string ToDisplayMessage(Exception exception)
    {
        var rawMessage = FlattenExceptionMessage(exception);
        var normalizedMessage = Normalize(rawMessage);
        var lowerMessage = normalizedMessage.ToLowerInvariant();

        if (IsAlreadyTranslated(lowerMessage))
        {
            return normalizedMessage;
        }

        if (TryGetLoopbackPort(normalizedMessage, out var port))
        {
            return $"Local port {port} is already in use. Change the port in Settings or close the program that is using it, then connect again.";
        }

        if (lowerMessage.Contains("failed to update cn domain rule-set") ||
            lowerMessage.Contains("failed to update cn ip rule-set") ||
            lowerMessage.Contains("rule-set"))
        {
            return "Rule-set update failed. Check the network first; if you are using Rule mode, connect once with an existing local rule-set or switch to Global/Direct, then run Update Rules again. " +
                   $"Detail: {Shorten(normalizedMessage)}";
        }

        if (lowerMessage.Contains("legacy dns servers is deprecated"))
        {
            return "The bundled sing-box rejected the generated DNS config because legacy DNS server format is no longer accepted. Install the latest EasyNaive package with the migrated DNS config. " +
                   $"Detail: {Shorten(normalizedMessage)}";
        }

        if (lowerMessage.Contains("detour to an empty direct outbound makes no sense"))
        {
            return "sing-box rejected the DNS detour configuration. This usually means the generated config is incompatible with the bundled sing-box version. Update EasyNaive and check the active config preview. " +
                   $"Detail: {Shorten(normalizedMessage)}";
        }

        if (lowerMessage.Contains("sing-box check failed"))
        {
            return $"sing-box configuration check failed. Open Diagnostics to inspect the active config, then check logs for the exact fatal line. Detail: {Shorten(normalizedMessage)}";
        }

        if (lowerMessage.Contains("sing-box exited unexpectedly during startup") ||
            lowerMessage.Contains("start service:") ||
            lowerMessage.Contains("failed to start sing-box"))
        {
            return $"sing-box exited during startup. Open Logs and look for the first FATAL/ERROR line; common causes are invalid config, occupied ports, or missing runtime files. Detail: {Shorten(normalizedMessage)}";
        }

        if (lowerMessage.Contains("clash api did not become available in time"))
        {
            return "sing-box started but the local controller API did not become available. Check whether the Clash API port is occupied, then open Logs for the startup error.";
        }

        if (lowerMessage.Contains("elevation helper") ||
            lowerMessage.Contains("easynaive service") ||
            lowerMessage.Contains("tun helper") ||
            lowerMessage.Contains("tun mode"))
        {
            return $"TUN helper/service failed. Make sure EasyNaiveService is installed and running; if it still fails, repair or reinstall the MSI. Detail: {Shorten(normalizedMessage)}";
        }

        if (lowerMessage.Contains("windows internet settings registry key") ||
            lowerMessage.Contains("system proxy"))
        {
            return $"Failed to update Windows system proxy settings. Check Windows permissions and whether another proxy manager is changing the same settings. Detail: {Shorten(normalizedMessage)}";
        }

        if (exception is TaskCanceledException ||
            exception.GetBaseException() is TimeoutException ||
            lowerMessage.Contains("timeout") ||
            lowerMessage.Contains("timed out"))
        {
            return $"The operation timed out. Check network connectivity, node reachability, and local proxy settings. Detail: {Shorten(normalizedMessage)}";
        }

        if (ContainsSocketError(exception, SocketError.ConnectionReset) ||
            lowerMessage.Contains("forcibly closed") ||
            lowerMessage.Contains("10054"))
        {
            return $"The remote host reset the connection. This is usually a network, server, or TLS/SNI mismatch issue. Detail: {Shorten(normalizedMessage)}";
        }

        if (exception is HttpRequestException || ContainsException<HttpRequestException>(exception))
        {
            return $"Network request failed. Check DNS, firewall, proxy state, or the remote URL. Detail: {Shorten(normalizedMessage)}";
        }

        if (lowerMessage.Contains("sing-box executable was not found") ||
            lowerMessage.Contains("missing:") ||
            exception is FileNotFoundException)
        {
            return $"Required runtime file is missing. Reinstall or repair EasyNaive, then run Self Check. Detail: {Shorten(normalizedMessage)}";
        }

        return normalizedMessage;
    }

    public static string ToDisplayMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        return ToDisplayMessage(new InvalidOperationException(message));
    }

    private static string FlattenExceptionMessage(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message))
            {
                messages.Add(current.Message);
            }
        }

        return string.Join(" ", messages);
    }

    private static bool TryGetLoopbackPort(string message, out string port)
    {
        var match = LoopbackPortRegex().Match(message);
        port = match.Success ? match.Value : string.Empty;
        return match.Success && message.Contains("already in use", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsSocketError(Exception exception, SocketError socketError)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SocketException socketException && socketException.SocketErrorCode == socketError)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsException<TException>(Exception exception)
        where TException : Exception
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is TException)
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string message)
    {
        var withoutAnsi = AnsiRegex().Replace(message, string.Empty);
        return WhiteSpaceRegex().Replace(withoutAnsi, " ").Trim();
    }

    private static bool IsAlreadyTranslated(string lowerMessage)
    {
        return lowerMessage.StartsWith("local port ", StringComparison.Ordinal) ||
               lowerMessage.StartsWith("rule-set update failed.", StringComparison.Ordinal) ||
               lowerMessage.StartsWith("the bundled sing-box rejected", StringComparison.Ordinal) ||
               lowerMessage.StartsWith("sing-box rejected", StringComparison.Ordinal) ||
               lowerMessage.StartsWith("sing-box configuration check failed.", StringComparison.Ordinal) ||
               lowerMessage.StartsWith("sing-box exited during startup.", StringComparison.Ordinal) ||
               lowerMessage.StartsWith("sing-box started but the local controller api", StringComparison.Ordinal) ||
               lowerMessage.StartsWith("tun helper/service failed.", StringComparison.Ordinal) ||
               lowerMessage.StartsWith("failed to update windows system proxy", StringComparison.Ordinal) ||
               lowerMessage.StartsWith("the operation timed out.", StringComparison.Ordinal) ||
               lowerMessage.StartsWith("the remote host reset", StringComparison.Ordinal) ||
               lowerMessage.StartsWith("network request failed.", StringComparison.Ordinal) ||
               lowerMessage.StartsWith("required runtime file is missing.", StringComparison.Ordinal);
    }

    private static string Shorten(string message)
    {
        const int maxLength = 260;
        return message.Length <= maxLength ? message : message[..maxLength] + "...";
    }

    [GeneratedRegex(@"127\.0\.0\.1:\d+")]
    private static partial Regex LoopbackPortRegex();

    [GeneratedRegex(@"\x1B\[[0-?]*[ -/]*[@-~]")]
    private static partial Regex AnsiRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpaceRegex();
}
