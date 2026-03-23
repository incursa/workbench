using System.Diagnostics;

namespace Workbench;

internal static class StartupBrowserLauncher
{
    internal static string? TryResolveLaunchUrl(ICollection<string> applicationUrls)
    {
        if (applicationUrls.Count == 0)
        {
            return null;
        }

        var preferredAddress = applicationUrls
            .Select(address => Uri.TryCreate(address, UriKind.Absolute, out var parsedAddress) ? parsedAddress : null)
            .Where(address => address is not null)
            .OrderByDescending(address => string.Equals(address!.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            .ThenBy(address => string.Equals(address!.Host, "localhost", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .FirstOrDefault();

        if (preferredAddress is null)
        {
            return applicationUrls.FirstOrDefault();
        }

        var host = preferredAddress.IsLoopback || !IsWildcardHost(preferredAddress.Host)
            ? preferredAddress.Host
            : "localhost";
        var builder = new UriBuilder(preferredAddress)
        {
            Host = host
        };

        return builder.Uri.ToString();
    }

    internal static bool TryLaunch(string url, out string failureReason)
    {
        try
        {
            using var process = Process.Start(CreateStartInfo(url));
            if (process is null)
            {
                failureReason = "The operating system did not start a browser process.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            failureReason = ex.ToString();
            return false;
        }
    }

    private static ProcessStartInfo CreateStartInfo(string url)
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = $"/c start \"\" \"{url}\"",
                CreateNoWindow = true
            };
        }

        if (OperatingSystem.IsMacOS())
        {
            return new ProcessStartInfo
            {
                FileName = "open",
                Arguments = url
            };
        }

        return new ProcessStartInfo
        {
            FileName = "xdg-open",
            Arguments = url
        };
    }

    private static bool IsWildcardHost(string host)
    {
        return string.Equals(host, "0.0.0.0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "[::]", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "+", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "*", StringComparison.OrdinalIgnoreCase);
    }
}
