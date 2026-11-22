using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Soundboard.Core.Models;

namespace Soundboard.App.WebRemote;

/// <summary>
/// Hosts <see cref="WebRemoteRequestHandler"/> on <c>http://localhost:&lt;port&gt;/</c> with HttpListener.
/// Localhost only: listening on the network would need administrator rights and expose the board
/// to the LAN; Stream Deck and scripts run on the same PC anyway.
/// </summary>
public sealed class WebRemoteServer(WebRemoteRequestHandler handler, ILogger<WebRemoteServer> logger) : IDisposable
{
    private readonly Lock _lock = new();
    private HttpListener? _listener;
    private CancellationTokenSource? _stopping;
    private int _port;
    private string? _token;

    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _listener is not null;
            }
        }
    }

    /// <summary>Starts, restarts or stops the server so it matches the settings.</summary>
    public void Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var wanted = settings.Features.WebRemote && !string.IsNullOrEmpty(settings.WebRemote.Token);
        lock (_lock)
        {
            if (wanted && _listener is not null && _port == settings.WebRemote.Port && _token == settings.WebRemote.Token)
            {
                return;
            }

            StopLocked();
            if (wanted)
            {
                StartLocked(settings.WebRemote.Port, settings.WebRemote.Token!);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            StopLocked();
        }
    }

    private void StartLocked(int port, string token)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            // Port taken by another program: the board keeps working, only the remote is off.
            logger.LogError(ex, "Web remote could not listen on port {Port}", port);
            listener.Close();
            return;
        }

        _listener = listener;
        _port = port;
        _token = token;
        _stopping = new CancellationTokenSource();
        _ = AcceptLoopAsync(listener, token, _stopping.Token);
        logger.LogInformation("Web remote listening on http://localhost:{Port}/", port);
    }

    private void StopLocked()
    {
        if (_listener is null)
        {
            return;
        }

        _stopping?.Cancel();
        _stopping?.Dispose();
        _stopping = null;
        _listener.Close();
        _listener = null;
        logger.LogInformation("Web remote stopped");
    }

    private async Task AcceptLoopAsync(HttpListener listener, string token, CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                // Listener closed by StopLocked.
                return;
            }

            _ = Task.Run(() => Serve(context, token), stopping);
        }
    }

    private void Serve(HttpListenerContext context, string token)
    {
        var response = context.Response;
        try
        {
            var request = context.Request;
            var query = request.QueryString.AllKeys
                .Where(k => k is not null)
                .ToDictionary(k => k!, k => request.QueryString[k] ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            var result = handler.Handle(
                new WebRemoteRequest(request.HttpMethod, request.Url?.AbsolutePath ?? "/", query, request.UserHostName ?? string.Empty, request.Headers[WebRemoteRequestHandler.TokenHeader]),
                token);

            var body = Encoding.UTF8.GetBytes(result.Body);
            response.StatusCode = result.StatusCode;
            response.ContentType = result.ContentType;
            response.Headers["Cache-Control"] = "no-store";
            response.OutputStream.Write(body);
        }
        catch (Exception ex)
        {
            // One bad request (or a failing action) must not take the remote down.
            logger.LogError(ex, "Web remote request failed");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }
}
