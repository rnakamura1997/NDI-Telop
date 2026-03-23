using System.Collections;
using Xunit;

namespace NdiTelop.Tests;

public sealed class ProgramDesktopLifetimeTests
{
    [Fact]
    public void CanStartClassicDesktopLifetime_ShouldReturnFalse_WhenHeadlessEnvironmentIsDetected()
    {
        IDictionary environment = new Hashtable
        {
            ["CI"] = "true",
            ["SESSIONNAME"] = "Console"
        };

        var result = DesktopStartupEnvironment.CanStartClassicDesktopLifetime(environment, isUserInteractive: true, sessionName: "Console");

        Assert.False(result);
    }

    [Fact]
    public void CanStartClassicDesktopLifetime_ShouldReturnFalse_OnNonWindowsPlatforms()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        IDictionary environment = new Hashtable
        {
            ["DISPLAY"] = ":0"
        };

        var result = DesktopStartupEnvironment.CanStartClassicDesktopLifetime(environment, isUserInteractive: true, sessionName: null);

        Assert.False(result);
    }

    [Fact]
    public void CanStartClassicDesktopLifetime_ShouldReturnFalse_WhenWindowsSessionIsNonInteractive()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        IDictionary environment = new Hashtable
        {
            ["SESSIONNAME"] = "Services"
        };

        var result = DesktopStartupEnvironment.CanStartClassicDesktopLifetime(environment, isUserInteractive: false, sessionName: "Services");

        Assert.False(result);
    }

    [Theory]
    [InlineData("Console")]
    [InlineData("RDP-Tcp#12")]
    [InlineData("ICA-Citrix")]
    public void CanStartClassicDesktopLifetime_ShouldReturnTrue_WhenWindowsSessionIsInteractive(string sessionName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        IDictionary environment = new Hashtable
        {
            ["SESSIONNAME"] = sessionName
        };

        var result = DesktopStartupEnvironment.CanStartClassicDesktopLifetime(environment, isUserInteractive: true, sessionName: sessionName);

        Assert.True(result);
    }

    [Fact]
    public void CanStartClassicDesktopLifetime_ShouldReturnFalse_WhenWindowsSessionNameIsUnsupported()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        IDictionary environment = new Hashtable
        {
            ["SESSIONNAME"] = "Service-0x0-3e7$"
        };

        var result = DesktopStartupEnvironment.CanStartClassicDesktopLifetime(environment, isUserInteractive: true, sessionName: "Service-0x0-3e7$");

        Assert.False(result);
    }

    [Fact]
    public void CanStartClassicDesktopLifetime_ShouldAllowInteractiveWindowsSessionWithoutSessionName()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        IDictionary environment = new Hashtable();

        var result = DesktopStartupEnvironment.CanStartClassicDesktopLifetime(environment, isUserInteractive: true, sessionName: null);

        Assert.True(result);
    }
}
