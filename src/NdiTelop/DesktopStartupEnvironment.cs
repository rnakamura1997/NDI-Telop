using System.Collections;

namespace NdiTelop;

internal static class DesktopStartupEnvironment
{
    private static readonly string[] InteractiveWindowsSessionPrefixes = ["Console", "RDP-", "ICA-"];
    private static readonly string[] NonInteractiveWindowsSessionPrefixes = ["Service-", "Services"];
    private static readonly string[] HeadlessEnvironmentVariables = ["CI", "GITHUB_ACTIONS", "DOTNET_RUNNING_IN_CONTAINER"];

    public static bool CanStartClassicDesktopLifetime()
        => CanStartClassicDesktopLifetime(
            Environment.GetEnvironmentVariables(),
            Environment.UserInteractive,
            Environment.GetEnvironmentVariable("SESSIONNAME"));

    internal static bool CanStartClassicDesktopLifetime(
        IDictionary environmentVariables,
        bool isUserInteractive,
        string? sessionName)
    {
        if (!OperatingSystem.IsWindows() || !isUserInteractive || IsHeadlessEnvironment(environmentVariables))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(sessionName))
        {
            return true;
        }

        if (HasSessionPrefix(sessionName, NonInteractiveWindowsSessionPrefixes))
        {
            return false;
        }

        return HasSessionPrefix(sessionName, InteractiveWindowsSessionPrefixes);
    }

    private static bool IsHeadlessEnvironment(IDictionary environmentVariables)
        => HeadlessEnvironmentVariables.Any(variableName => HasTruthyEnvironmentVariable(environmentVariables, variableName));

    private static bool HasSessionPrefix(string sessionName, IEnumerable<string> prefixes)
        => prefixes.Any(prefix =>
            sessionName.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || sessionName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool HasTruthyEnvironmentVariable(IDictionary environmentVariables, string key)
        => TryGetEnvironmentVariable(environmentVariables, key, out var value)
            && (value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("1", StringComparison.OrdinalIgnoreCase));

    private static bool TryGetEnvironmentVariable(IDictionary environmentVariables, string key, out string value)
    {
        value = string.Empty;
        if (!environmentVariables.Contains(key))
        {
            return false;
        }

        value = environmentVariables[key]?.ToString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }
}
