using Microsoft.Extensions.DependencyInjection;
using NdiTelop.Interfaces;
using NdiTelop.Services;
using NdiTelop.ViewModels;
using Xunit;

namespace NdiTelop.Tests;

public class ProgramServiceRegistrationTests
{
    [Fact]
    public void ServiceRegistration_ShouldResolveRenderServiceAndMainWindowViewModel()
    {
        var services = new ServiceCollection();
        var registerServices = typeof(Program).GetMethod("RegisterServices", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(registerServices);

        registerServices!.Invoke(null, [services]);

        using var provider = services.BuildServiceProvider();
        var renderService = provider.GetRequiredService<RenderService>();
        var renderServiceFromInterface = provider.GetRequiredService<IRenderService>();
        var viewModel = provider.GetRequiredService<MainWindowViewModel>();
        var startupService = provider.GetRequiredService<ApplicationStartupService>();

        Assert.Same(renderService, renderServiceFromInterface);
        Assert.NotNull(viewModel);
        Assert.NotNull(startupService);
    }
}
