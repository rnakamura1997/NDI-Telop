using System.Net;
using System.Text;
using System.Text.Json;
using NdiTelop.Interfaces;
using NdiTelop.Models;
using NdiTelop.Services.WebUi;
using Serilog;

namespace NdiTelop.Services;

public class WebApiService : IWebApiService
{
    private readonly ExternalControlCoordinator _coordinator;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _serverTask;

    public WebApiService(ExternalControlCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public int Port { get; set; } = 5000;
    public string Host { get; set; } = "*";

    public Task StartAsync()
    {
        if (_serverTask != null && !_serverTask.IsCompleted)
        {
            return Task.CompletedTask;
        }

        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://{Host}:{Port}/");
            _listener.Start();

            _cts = new CancellationTokenSource();
            _serverTask = RunServerAsync(_cts.Token);
            Log.Information("Web API listener started. Host={Host}, Port={Port}", Host, Port);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Web API listener failed to start. Host={Host}, Port={Port}", Host, Port);
            _listener?.Close();
            _listener = null;
            _cts?.Dispose();
            _cts = null;
            _serverTask = null;
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_cts == null)
        {
            return;
        }

        _cts.Cancel();
        _listener?.Stop();

        if (_serverTask != null)
        {
            try
            {
                await _serverTask;
            }
            catch (OperationCanceledException)
            {
                // no-op
            }
            catch (HttpListenerException)
            {
                // no-op
            }
        }

        _listener?.Close();
        _listener = null;
        _cts.Dispose();
        _cts = null;
        _serverTask = null;
        Log.Information("Web API listener stopped.");
    }

    private async Task RunServerAsync(CancellationToken cancellationToken)
    {
        if (_listener == null)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            _ = Task.Run(() => HandleRequestSafeAsync(context), CancellationToken.None);
        }
    }

    private async Task HandleRequestSafeAsync(HttpListenerContext context)
    {
        try
        {
            await HandleRequestAsync(context);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled Web API request failure. Path={Path}", context.Request.Url?.AbsolutePath);
            await TryWriteErrorResponseAsync(context.Response, HttpStatusCode.InternalServerError, new { message = "Internal server error." });
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var path = request.Url?.AbsolutePath?.TrimEnd('/') ?? string.Empty;

            if (request.HttpMethod == HttpMethod.Get.Method && (string.IsNullOrEmpty(path) || path.Equals("/index.html", StringComparison.OrdinalIgnoreCase)))
            {
                await WriteTextAsync(context.Response, HttpStatusCode.OK, "text/html; charset=utf-8", WebUiStaticContent.IndexHtml);
                return;
            }

            if (request.HttpMethod == HttpMethod.Get.Method && path.Equals("/web-ui.css", StringComparison.OrdinalIgnoreCase))
            {
                await WriteTextAsync(context.Response, HttpStatusCode.OK, "text/css; charset=utf-8", WebUiStaticContent.StylesCss);
                return;
            }

            if (request.HttpMethod == HttpMethod.Get.Method && path.Equals("/web-ui.js", StringComparison.OrdinalIgnoreCase))
            {
                await WriteTextAsync(context.Response, HttpStatusCode.OK, "application/javascript; charset=utf-8", WebUiStaticContent.ScriptJs);
                return;
            }

            if (request.HttpMethod == HttpMethod.Get.Method && path.Equals("/api/presets", StringComparison.OrdinalIgnoreCase))
            {
                var data = _coordinator.GetPresets().Select(p => new { p.Id, p.Name });
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, data);
                return;
            }

            if (request.HttpMethod == HttpMethod.Get.Method && path.Equals("/api/status/ndi", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, new { status = _coordinator.GetNdiOutputStatus() });
                return;
            }

            if (request.HttpMethod == HttpMethod.Get.Method && path.Equals("/api/playlist/status", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, _coordinator.GetPlaylistSnapshot());
                return;
            }

            if (request.HttpMethod == HttpMethod.Get.Method && path.Equals("/api/settings/basic", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, _coordinator.GetBasicSettings());
                return;
            }

            if (request.HttpMethod == HttpMethod.Get.Method && path.Equals("/api/remote-control/settings", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, _coordinator.GetRemoteControlSettings());
                return;
            }

            if (request.HttpMethod == HttpMethod.Post.Method && path.Equals("/api/program/clear", StringComparison.OrdinalIgnoreCase))
            {
                var cleared = await _coordinator.ClearProgramAsync();
                if (!cleared)
                {
                    await WriteJsonAsync(context.Response, HttpStatusCode.NotFound, new { message = "Program output is not available." });
                    return;
                }

                await WriteJsonAsync(context.Response, HttpStatusCode.OK, new { message = "Program output cleared." });
                return;
            }

            if (request.HttpMethod == HttpMethod.Post.Method && path.Equals("/api/playlist/next-cue", StringComparison.OrdinalIgnoreCase))
            {
                var advanced = await _coordinator.TriggerNextCueAsync();
                await WriteJsonAsync(context.Response, advanced ? HttpStatusCode.OK : HttpStatusCode.NotFound, new { message = advanced ? "Next cue triggered." : "Next cue handler unavailable." });
                return;
            }

            if (request.HttpMethod == HttpMethod.Post.Method && (path.Equals("/api/take", StringComparison.OrdinalIgnoreCase) || path.Equals("/take", StringComparison.OrdinalIgnoreCase)))
            {
                var payload = await ReadJsonAsync<TakeRequest>(request);
                if (payload == null || string.IsNullOrWhiteSpace(payload.PresetId))
                {
                    await WriteJsonAsync(context.Response, HttpStatusCode.BadRequest, new { message = "PresetId is required." });
                    return;
                }

                var taken = await _coordinator.TakePresetByIdAsync(payload.PresetId);
                if (!taken)
                {
                    await WriteJsonAsync(context.Response, HttpStatusCode.NotFound, new { message = "Preset not found or take handler unavailable." });
                    return;
                }

                await WriteJsonAsync(context.Response, HttpStatusCode.OK, new { message = "Preset taken.", payload.PresetId });
                return;
            }

            if (request.HttpMethod == HttpMethod.Post.Method && path.StartsWith("/api/presets/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/activate", StringComparison.OrdinalIgnoreCase))
            {
                var id = path["/api/presets/".Length..^"/activate".Length].Trim('/');
                if (string.IsNullOrWhiteSpace(id))
                {
                    await WriteJsonAsync(context.Response, HttpStatusCode.BadRequest, new { message = "Preset id is required." });
                    return;
                }

                var activated = await _coordinator.ShowPresetByIdAsync(id);
                if (!activated)
                {
                    await WriteJsonAsync(context.Response, HttpStatusCode.NotFound, new { message = "Preset not found or not available." });
                    return;
                }

                await WriteJsonAsync(context.Response, HttpStatusCode.OK, new { message = "Preset activated.", id });
                return;
            }

            if (request.HttpMethod == HttpMethod.Post.Method && path.StartsWith("/api/keyers/", StringComparison.OrdinalIgnoreCase))
            {
                var destinationToken = path["/api/keyers/".Length..].Trim('/');
                if (!TryResolveKeyerRoute(destinationToken, out var destination, out var action))
                {
                    await WriteJsonAsync(context.Response, HttpStatusCode.BadRequest, new { message = "Keyer route is invalid." });
                    return;
                }

                if (string.Equals(action, "auto", StringComparison.OrdinalIgnoreCase))
                {
                    var autoResult = await _coordinator.RunKeyerAutoAsync(destination);
                    await WriteJsonAsync(context.Response, autoResult ? HttpStatusCode.OK : HttpStatusCode.NotFound, new
                    {
                        message = autoResult ? "Keyer AUTO executed." : "Keyer AUTO handler unavailable.",
                        destination = destination.ToDisplayName()
                    });
                    return;
                }

                var payload = await ReadJsonAsync<KeyerControlRequest>(request) ?? new KeyerControlRequest();
                var requestedState = action switch
                {
                    "on" => true,
                    "off" => false,
                    "toggle" => null,
                    _ => payload.KeyOn
                };

                var stateApplied = await _coordinator.SetKeyerStateAsync(destination, requestedState, payload.Opacity);
                await WriteJsonAsync(context.Response, stateApplied ? HttpStatusCode.OK : HttpStatusCode.NotFound, new
                {
                    message = stateApplied ? "Keyer updated." : "Keyer handler unavailable.",
                    destination = destination.ToDisplayName(),
                    keyOn = requestedState,
                    payload.Opacity
                });
                return;
            }

            if (request.HttpMethod == HttpMethod.Post.Method && path.Equals("/api/tally", StringComparison.OrdinalIgnoreCase))
            {
                var signal = await ReadJsonAsync<TallySignal>(request);
                if (signal == null)
                {
                    await WriteJsonAsync(context.Response, HttpStatusCode.BadRequest, new { message = "Tally payload is invalid." });
                    return;
                }

                signal.RemoteIpAddress = request.RemoteEndPoint?.Address.ToString() ?? signal.RemoteIpAddress;
                signal.Transport = string.IsNullOrWhiteSpace(signal.Transport) ? "http" : signal.Transport;
                signal.ReceivedAt = DateTimeOffset.UtcNow;

                var autoTakeTriggered = await _coordinator.ApplyTallySignalAsync(signal);
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, new { message = "Tally processed.", autoTakeTriggered });
                return;
            }

            if (request.HttpMethod == HttpMethod.Post.Method && path.Equals("/api/tally/ndi-metadata", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
                var metadata = await reader.ReadToEndAsync();
                var signal = ParseNdiMetadataTally(metadata, request.RemoteEndPoint?.Address.ToString() ?? string.Empty);
                if (signal == null)
                {
                    await WriteJsonAsync(context.Response, HttpStatusCode.BadRequest, new { message = "NDI metadata tally payload is invalid." });
                    return;
                }

                var autoTakeTriggered = await _coordinator.ApplyTallySignalAsync(signal);
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, new { message = "NDI metadata tally processed.", autoTakeTriggered });
                return;
            }

            await WriteJsonAsync(context.Response, HttpStatusCode.NotFound, new { message = "Not found." });
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "Web API request JSON was invalid. Path={Path}", context.Request.Url?.AbsolutePath);
            await TryWriteErrorResponseAsync(context.Response, HttpStatusCode.BadRequest, new { message = "Request payload is invalid." });
        }
        catch (HttpListenerException ex)
        {
            Log.Warning(ex, "Web API response channel closed unexpectedly. Path={Path}", context.Request.Url?.AbsolutePath);
        }
        catch (ObjectDisposedException ex)
        {
            Log.Warning(ex, "Web API response disposed before completion. Path={Path}", context.Request.Url?.AbsolutePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Web API request failed. Path={Path}", context.Request.Url?.AbsolutePath);
            await TryWriteErrorResponseAsync(context.Response, HttpStatusCode.InternalServerError, new { message = "Internal server error." });
        }
    }

    private async Task TryWriteErrorResponseAsync(HttpListenerResponse response, HttpStatusCode statusCode, object payload)
    {
        try
        {
            if (response.OutputStream.CanWrite)
            {
                await WriteJsonAsync(response, statusCode, payload);
            }
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
        {
            Log.Warning(ex, "Skipped error response because the HTTP client disconnected.");
        }
    }

    private static async Task WriteTextAsync(HttpListenerResponse response, HttpStatusCode statusCode, string contentType, string payload)
    {
        response.StatusCode = (int)statusCode;
        response.ContentType = contentType;

        var bytes = Encoding.UTF8.GetBytes(payload);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.OutputStream.Close();
    }

    private async Task WriteJsonAsync(HttpListenerResponse response, HttpStatusCode statusCode, object payload)
    {
        response.StatusCode = (int)statusCode;
        response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.OutputStream.Close();
    }

    private async Task<T?> ReadJsonAsync<T>(HttpListenerRequest request)
    {
        if (!request.HasEntityBody)
        {
            return default;
        }

        return await JsonSerializer.DeserializeAsync<T>(request.InputStream, _jsonOptions);
    }

    private static bool TryResolveKeyerRoute(string route, out KeyerDestination destination, out string action)
    {
        destination = KeyerDestination.Usk1;
        action = string.Empty;

        var parts = route.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || !KeyerDestinationParser.TryParse(parts[0], out destination))
        {
            return false;
        }

        action = parts.Length > 1 ? parts[1].ToLowerInvariant() : "state";
        return true;
    }

    private static TallySignal? ParseNdiMetadataTally(string metadata, string remoteIpAddress)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return null;
        }

        var normalized = metadata.ToLowerInvariant();
        var program = normalized.Contains("program=\"true\"", StringComparison.Ordinal) ||
                      normalized.Contains("<program>true</program>", StringComparison.Ordinal) ||
                      normalized.Contains("on_program=\"true\"", StringComparison.Ordinal);
        var preview = normalized.Contains("preview=\"true\"", StringComparison.Ordinal) ||
                      normalized.Contains("<preview>true</preview>", StringComparison.Ordinal);

        var source = ExtractAttribute(metadata, "source") ?? ExtractTagValue(metadata, "source") ?? "ndi-metadata";

        return new TallySignal
        {
            Source = source,
            RemoteIpAddress = remoteIpAddress,
            Transport = "ndi-metadata",
            Program = program,
            Preview = preview,
            Metadata = metadata,
            ReceivedAt = DateTimeOffset.UtcNow
        };
    }

    private static string? ExtractAttribute(string input, string attributeName)
    {
        var marker = $"{attributeName}=\"";
        var index = input.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var start = index + marker.Length;
        var end = input.IndexOf('"', start);
        return end > start ? input[start..end] : null;
    }

    private static string? ExtractTagValue(string input, string tagName)
    {
        var startTag = $"<{tagName}>";
        var endTag = $"</{tagName}>";
        var start = input.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
        var end = input.IndexOf(endTag, StringComparison.OrdinalIgnoreCase);
        if (start < 0 || end <= start)
        {
            return null;
        }

        start += startTag.Length;
        return input[start..end].Trim();
    }
}
