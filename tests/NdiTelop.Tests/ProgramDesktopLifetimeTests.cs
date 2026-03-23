using System.Collections;
using Xunit;

namespace NdiTelop.Tests;

public sealed class ProgramDesktopLifetimeTests
{
    [Fact]
    public void CanStartDesktopLifetime_ShouldReturnFalse_WhenCiEnvironmentIsDetected()
    {
        IDictionary environment = new Hashtable
        {
            ["CI"] = "true",
            ["DISPLAY"] = ":0",
            ["SESSIONNAME"] = "Console"
        };

        var result = Program.CanStartDesktopLifetime(environment, isUserInteractive: true, sessionName: "Console");

        Assert.False(result);
    }

    [Fact]
    public void CanStartDesktopLifetime_ShouldReturnFalse_WhenLinuxDisplayVariablesAreMissing()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        IDictionary environment = new Hashtable();

        var result = Program.CanStartDesktopLifetime(environment, isUserInteractive: true, sessionName: null);

        Assert.False(result);
    }

    [Fact]
    public void CanStartDesktopLifetime_ShouldReturnTrue_WhenLinuxDisplayVariableExists()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        IDictionary environment = new Hashtable
        {
            ["DISPLAY"] = ":0"
        };

        var result = Program.CanStartDesktopLifetime(environment, isUserInteractive: true, sessionName: null);

        Assert.True(result);
    }

    [Fact]
    public void CanStartDesktopLifetime_ShouldReturnFalse_WhenWindowsSessionIsNonInteractive()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        IDictionary environment = new Hashtable
        {
            ["SESSIONNAME"] = "Services"
        };

        var result = Program.CanStartDesktopLifetime(environment, isUserInteractive: false, sessionName: "Services");

        Assert.False(result);
    }

    [Fact]
    public void CanStartDesktopLifetime_ShouldReturnTrue_WhenWindowsSessionIsInteractive()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        IDictionary environment = new Hashtable
        {
            ["SESSIONNAME"] = "Console"
        };

        var result = Program.CanStartDesktopLifetime(environment, isUserInteractive: true, sessionName: "Console");

        Assert.True(result);
    }
}
