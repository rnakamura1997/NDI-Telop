using NdiTelop.Interfaces;
using NdiTelop.Services.Output;
using Serilog;

namespace NdiTelop.Services;

public class OutputService : IOutputService
{
    private readonly BackendState _virtualCameraState;
    private readonly BackendState _deckLinkState;
    private readonly BackendState _spoutState;

    public OutputService()
        : this(
            new NoOpOutputBackend("VirtualCamera"),
            new NoOpOutputBackend("DeckLink"),
            new Spout2PocOutputBackend())
    {
    }

    public OutputService(IOutputBackend virtualCameraBackend, IOutputBackend deckLinkBackend, IOutputBackend spoutBackend)
    {
        _virtualCameraState = new BackendState(virtualCameraBackend);
        _deckLinkState = new BackendState(deckLinkBackend);
        _spoutState = new BackendState(spoutBackend);
    }

    public Task StartVirtualCameraAsync() => StartAsync(_virtualCameraState, new OutputStartContext());

    public Task StopVirtualCameraAsync() => StopAsync(_virtualCameraState);

    public Task StartDeckLinkOutputAsync(int deviceIndex)
    {
        if (deviceIndex < 0)
        {
            Log.Warning("Output start skipped: DeckLink device index is invalid. DeviceIndex={DeviceIndex}", deviceIndex);
            return Task.CompletedTask;
        }

        return StartAsync(_deckLinkState, new OutputStartContext(DeviceIndex: deviceIndex));
    }

    public Task StopDeckLinkOutputAsync() => StopAsync(_deckLinkState);

    public Task StartSpoutAsync(string senderName)
    {
        if (string.IsNullOrWhiteSpace(senderName))
        {
            Log.Warning("Output start skipped: Spout sender name is empty.");
            return Task.CompletedTask;
        }

        return StartAsync(_spoutState, new OutputStartContext(SenderName: senderName));
    }

    public Task StopSpoutAsync() => StopAsync(_spoutState);

    public IReadOnlyList<string> GetAvailableDeckLinkDevices() => _deckLinkState.Backend.GetAvailableDevices();

    public Task SendVirtualCameraFrameAsync(ReadOnlyMemory<byte> payload) => SendAsync(_virtualCameraState, payload);

    public Task SendDeckLinkFrameAsync(ReadOnlyMemory<byte> payload) => SendAsync(_deckLinkState, payload);

    public Task SendSpoutFrameAsync(ReadOnlyMemory<byte> payload) => SendAsync(_spoutState, payload);

    private static async Task StartAsync(BackendState state, OutputStartContext context)
    {
        await state.Gate.WaitAsync();
        try
        {
            if (state.LifecycleState is OutputLifecycleState.Starting or OutputLifecycleState.Started)
            {
                Log.Warning("Output start skipped: {Backend} is already active. CurrentState={State}", state.Backend.BackendName, state.LifecycleState);
                return;
            }

            state.LifecycleState = OutputLifecycleState.Starting;
            Log.Information("Output start requested: {Backend}", state.Backend.BackendName);

            try
            {
                await state.Backend.StartAsync(context);
                state.LifecycleState = OutputLifecycleState.Started;
                Log.Information("Output started: {Backend}", state.Backend.BackendName);
            }
            catch (Exception ex)
            {
                state.LifecycleState = OutputLifecycleState.NotStarted;
                Log.Warning(ex, "Output start failed: {Backend}", state.Backend.BackendName);
            }
        }
        finally
        {
            state.Gate.Release();
        }
    }

    private static async Task StopAsync(BackendState state)
    {
        await state.Gate.WaitAsync();
        try
        {
            if (state.LifecycleState is OutputLifecycleState.NotStarted or OutputLifecycleState.Stopped)
            {
                Log.Warning("Output stop skipped: {Backend} is not running. CurrentState={State}", state.Backend.BackendName, state.LifecycleState);
                state.LifecycleState = OutputLifecycleState.Stopped;
                return;
            }

            try
            {
                await state.Backend.StopAsync();
                Log.Information("Output stopped: {Backend}", state.Backend.BackendName);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Output stop failed: {Backend}", state.Backend.BackendName);
            }

            state.LifecycleState = OutputLifecycleState.Stopped;
        }
        finally
        {
            state.Gate.Release();
        }
    }

    private static async Task SendAsync(BackendState state, ReadOnlyMemory<byte> payload)
    {
        await state.Gate.WaitAsync();
        try
        {
            if (state.LifecycleState != OutputLifecycleState.Started)
            {
                Log.Warning("Output send skipped: {Backend} is not started. CurrentState={State}", state.Backend.BackendName, state.LifecycleState);
                return;
            }

            try
            {
                await state.Backend.SendAsync(payload);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Output send failed: {Backend}", state.Backend.BackendName);
            }
        }
        finally
        {
            state.Gate.Release();
        }
    }

    private sealed class BackendState(IOutputBackend backend)
    {
        public IOutputBackend Backend { get; } = backend;

        public OutputLifecycleState LifecycleState { get; set; }

        public SemaphoreSlim Gate { get; } = new(1, 1);
    }
}
