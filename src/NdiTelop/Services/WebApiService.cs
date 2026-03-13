using System.Net;
using System.Text;
using System.Text.Json;
using NdiTelop.Interfaces;
using NdiTelop.Services.WebUi;

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

    public Task StartAsync()
    {
        if (_serverTask != null && !_serverTask.IsCompleted)
        {
            return Task.CompletedTask;
        }

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://*:{Port}/");
        _listener.Start();

        _cts = new CancellationTokenSource();
        _serverTask = RunServerAsync(_cts.Token);
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

            _ = Task.Run(() => HandleRequestAsync(context), cancellationToken);
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

            await WriteJsonAsync(context.Response, HttpStatusCode.NotFound, new { message = "Not found." });
        }
        catch
        {
            if (context.Response.OutputStream.CanWrite)
            {
                await WriteJsonAsync(context.Response, HttpStatusCode.InternalServerError, new { message = "Internal server error." });
            }
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
}
