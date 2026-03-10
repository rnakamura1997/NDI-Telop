using NSubstitute;
using NdiTelop.Interfaces;

namespace NdiTelop.Tests.Mocks;

public static class MockServices
{
    public static INdiService CreateNdiService() => Substitute.For<INdiService>();
    public static IRenderService CreateRenderService() => Substitute.For<IRenderService>();
    public static IPresetService CreatePresetService() => Substitute.For<IPresetService>();
    public static ISetlistService CreateSetlistService() => Substitute.For<ISetlistService>();
    public static IWebApiService CreateWebApiService() => Substitute.For<IWebApiService>();
    public static IOscService CreateOscService() => Substitute.For<IOscService>();
    public static IOutputService CreateOutputService() => Substitute.For<IOutputService>();
    public static ISettingsService CreateSettingsService() => Substitute.For<ISettingsService>();
}
