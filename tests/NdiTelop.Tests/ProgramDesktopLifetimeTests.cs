using System.Collections;
using Xunit;

namespace NdiTelop.Tests;

public sealed class ProgramDesktopLifetimeTests
{
    [Fact]
    public void CanStartDesktopLifetime_ShouldReturnFalse_WhenLinuxDisplayVariablesAreMissing()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        IDictionary environment = new Hashtable();

        var result = Program.CanStartDesktopLifetime(environment);

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

        var result = Program.CanStartDesktopLifetime(environment);

        Assert.True(result);
    }
}
