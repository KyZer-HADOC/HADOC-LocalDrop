using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HADOC.LocalDrop.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;

namespace HADOC.LocalDrop.Services;

public sealed class LocalDropServer : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, SharedFile> _sharedFiles = new();
    private readonly ConcurrentDictionary<Guid, WebSocket> _sockets = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private WebApplication? _app;
    private string _token = "";
    private string? _pairedDeviceId;
    private string _pairedDeviceName = "Waiting for phone";
    private int _port;

    public event Action<ChatMessage>? ChatReceived;
    public event Action<string>? DeviceConnected;
    public event Action? DeviceDisconnected;
    public event Action<ReceivedFile>? FileReceived;
    public event Action<TransferProgress>? TransferProgressChanged;
    public event Action<string>? StatusChanged;

    public bool IsRunning => _app is not null;
    public bool IsPhoneConnected => !_sockets.IsEmpty;
    public bool IsPaired => _pairedDeviceId is not null;

    public string GetReceivedFolder()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads", "HADOC LocalDrop", "Received");
        Directory.CreateDirectory(folder);
        return folder;
    }

    public IReadOnlyList<ReceivedFile> GetReceivedFiles()
    {
        var folder = GetReceivedFolder();
        return Directory.EnumerateFiles(folder)
            .Select(path => new FileInfo(path))
            .Where(info => info.Exists)
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .Select(info => new ReceivedFile(Guid.NewGuid().ToString("N"), info.Name, info.FullName, info.Length, info.LastWriteTime))
            .ToArray();
    }

    public async Task DisconnectPhoneAsync()
    {
        _pairedDeviceId = null;
        _pairedDeviceName = "Waiting for phone";
        foreach (var socket in _sockets.Values)
        {
            try
            {
                if (socket.State == WebSocketState.Open)
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected by PC", CancellationToken.None);
            }
            catch (WebSocketException) { }
        }
        DeviceDisconnected?.Invoke();
        StatusChanged?.Invoke("Phone disconnected");
    }

    public IReadOnlyList<string> GetLocalAddresses()
    {
        var candidates = new List<(string Address, bool HasGateway, bool IsWifi)>();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up ||
                adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            try
            {
                var properties = adapter.GetIPProperties();
                var hasGateway = properties.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));

                foreach (var unicast in properties.UnicastAddresses)
                {
                    var address = unicast.Address;
                    if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
                        continue;
                    var text = address.ToString();
                    if (text.StartsWith("169.254.", StringComparison.Ordinal))
                        continue;
                    candidates.Add((text, hasGateway,
                        adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211));
                }
            }
            catch (NetworkInformationException) { }
        }

        return candidates.OrderByDescending(x => x.HasGateway)
            .ThenByDescending(x => x.IsWifi)
            .Select(x => x.Address).Distinct().ToArray();
    }

    public async Task StartAsync()
    {
        if (_app is not null) return;

        var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (!Directory.Exists(webRoot) || !File.Exists(Path.Combine(webRoot, "index.html")))
            throw new DirectoryNotFoundException("The phone interface was not found. Reinstall HADOC LocalDrop.");

        _token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        _pairedDeviceId = null;
        _pairedDeviceName = "Waiting for phone";
        _port = 43821;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = webRoot
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = null;
            options.ListenAnyIP(_port);
        });

        var app = builder.Build();
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(25) });
        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapGet("/api/health", () => Results.Ok(new { status = "online" }));

        app.MapPost("/api/connect", async context =>
        {
            if (!IsAuthorized(context))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var deviceId = context.Request.Headers["X-Device-Id"].ToString();
            var deviceName = context.Request.Headers["X-Device-Name"].ToString();
            if (string.IsNullOrWhiteSpace(deviceId) || deviceId.Length > 100)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            if (_pairedDeviceId is not null && !string.Equals(_pairedDeviceId, deviceId, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "This PC is already paired with another device. Disconnect it first."
                });
                return;
            }

            var isNewDevice = _pairedDeviceId is null;
            _pairedDeviceId = deviceId;
            _pairedDeviceName = string.IsNullOrWhiteSpace(deviceName)
                ? "Mobile device"
                : deviceName[..Math.Min(deviceName.Length, 60)];

            if (isNewDevice)
            {
                DeviceConnected?.Invoke(_pairedDeviceName);
                StatusChanged?.Invoke($"Connected to {_pairedDeviceName}");
            }

            await context.Response.WriteAsJsonAsync(new
            {
                ok = true,
                pcName = Environment.MachineName,
                deviceName = _pairedDeviceName
            });
        });

        app.MapGet("/api/files", async context =>
        {
            if (!IsAuthorized(context))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var files = _sharedFiles.Values.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .Select(f => new { id = f.Id, name = f.Name, size = f.Size, sizeLabel = SharedFile.FormatSize(f.Size) })
                .ToArray();
            await context.Response.WriteAsJsonAsync(files);
        });

        app.MapPost("/api/transfer/progress", async context =>
        {
            if (!IsAuthorized(context))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            TransferProgress? update;
            try
            {
                update = await JsonSerializer.DeserializeAsync<TransferProgress>(
                    context.Request.Body,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web),
                    context.RequestAborted);
            }
            catch (JsonException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            if (update is null ||
                string.IsNullOrWhiteSpace(update.Id) || update.Id.Length > 128 ||
                string.IsNullOrWhiteSpace(update.Name) || update.Name.Length > 260 ||
                update.Total < 0 || update.Loaded < 0 ||
                (update.Direction != "phone-to-pc" && update.Direction != "pc-to-phone") ||
                (update.State != "running" && update.State != "completed" && update.State != "failed"))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            TransferProgressChanged?.Invoke(update);
            context.Response.StatusCode = StatusCodes.Status204NoContent;
        });

        app.MapGet("/api/download/{id}", async context =>
        {
            if (!IsAuthorized(context))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var id = context.Request.RouteValues["id"]?.ToString() ?? "";
            if (!_sharedFiles.TryGetValue(id, out var file) || !File.Exists(file.Path))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await Results.File(file.Path, "application/octet-stream", file.Name, enableRangeProcessing: true)
                .ExecuteAsync(context);
        });

        app.MapPost("/api/upload", async context =>
        {
            if (!IsAuthorized(context))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Pairing expired. Scan the QR code again.");
                return;
            }

            var requestedName = context.Request.Query["name"].ToString();
            var safeName = Path.GetFileName(requestedName.Replace('/', '\\'));
            if (string.IsNullOrWhiteSpace(safeName) || safeName is "." or "..")
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Invalid file name.");
                return;
            }

            var transferId = context.Request.Query["transferId"].ToString();
            if (string.IsNullOrWhiteSpace(transferId) || transferId.Length > 128)
                transferId = Guid.NewGuid().ToString("N");

            var receiveFolder = GetReceivedFolder();
            var targetPath = GetUniquePath(receiveFolder, safeName);

            try
            {
                await using (var output = new FileStream(
                    targetPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 1024 * 1024,
                    options: FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await context.Request.Body.CopyToAsync(output, context.RequestAborted);
                    await output.FlushAsync(context.RequestAborted);
                }

                var info = new FileInfo(targetPath);
                var received = new ReceivedFile(transferId, info.Name, info.FullName, info.Length, info.LastWriteTime);
                FileReceived?.Invoke(received);
                TransferProgressChanged?.Invoke(new TransferProgress(
                    transferId, "phone-to-pc", info.Name, info.Length, info.Length, 0, "completed"));
                StatusChanged?.Invoke("Received " + info.Name);

                context.Response.StatusCode = StatusCodes.Status201Created;
                await context.Response.WriteAsJsonAsync(new
                {
                    ok = true,
                    name = received.Name,
                    size = received.Size,
                    sizeLabel = received.SizeLabel,
                    savedTo = receiveFolder
                });
            }
            catch (OperationCanceledException)
            {
                try { if (File.Exists(targetPath)) File.Delete(targetPath); } catch { }
                throw;
            }
            catch (Exception ex)
            {
                try { if (File.Exists(targetPath)) File.Delete(targetPath); } catch { }
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new
                {
                    ok = false,
                    error = "Could not save the uploaded file: " + ex.Message
                });
            }
        });

        app.MapPost("/api/disconnect", async context =>
        {
            if (!IsAuthorized(context))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await DisconnectPhoneAsync();
            StatusChanged?.Invoke("Phone disconnected");
            await context.Response.WriteAsJsonAsync(new { ok = true });
        });

        app.Map("/ws", async context =>
        {
            if (!IsAuthorized(context) || !context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = !IsAuthorized(context)
                    ? StatusCodes.Status401Unauthorized : StatusCodes.Status400BadRequest;
                return;
            }
            if (_pairedDeviceId is null)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var socketId = Guid.NewGuid();
            _sockets[socketId] = socket;
            StatusChanged?.Invoke($"Live connection: {_pairedDeviceName}");

            try
            {
                var buffer = new byte[4096];
                while (socket.State == WebSocketState.Open && !context.RequestAborted.IsCancellationRequested)
                {
                    using var messageStream = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);
                        if (result.MessageType == WebSocketMessageType.Close) return;
                        if (result.MessageType != WebSocketMessageType.Text) continue;
                        messageStream.Write(buffer, 0, result.Count);
                        if (messageStream.Length > 8192)
                            throw new InvalidDataException("Chat message is too large.");
                    } while (!result.EndOfMessage);

                    if (result.MessageType != WebSocketMessageType.Text) continue;
                    using var document = JsonDocument.Parse(messageStream.ToArray());
                    if (!document.RootElement.TryGetProperty("text", out var textElement)) continue;

                    var messageText = textElement.GetString()?.Trim();
                    if (string.IsNullOrWhiteSpace(messageText)) continue;
                    if (messageText.Length > 2000) messageText = messageText[..2000];

                    var message = new ChatMessage("Phone", messageText, DateTimeOffset.Now);
                    ChatReceived?.Invoke(message);
                    await BroadcastChatAsync(message, socket);
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException) { }
            catch (JsonException) { }
            catch (InvalidDataException) { }
            finally
            {
                _sockets.TryRemove(socketId, out _);
                if (_sockets.IsEmpty)
                {
                    StatusChanged?.Invoke(_pairedDeviceId is null
                        ? "Phone disconnected"
                        : "Phone connection paused — waiting for phone");
                    if (_pairedDeviceId is null)
                        DeviceDisconnected?.Invoke();
                }
            }
        });

        try
        {
            await app.StartAsync();
            _app = app;
            StatusChanged?.Invoke($"LocalDrop server started on port {_port}");
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    public string CreatePairUrl(string ipAddress)
    {
        if (_app is null) throw new InvalidOperationException("Start the LocalDrop server first.");
        return $"http://{ipAddress}:{_port}/?token={Uri.EscapeDataString(_token)}";
    }

    public SharedFile AddSharedFile(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("The selected file could not be found.", path);
        var item = new SharedFile(Guid.NewGuid().ToString("N"), info.Name, info.FullName, info.Length);
        _sharedFiles[item.Id] = item;
        return item;
    }

    public void RemoveSharedFile(string id) => _sharedFiles.TryRemove(id, out _);

    public Task SendDesktopChatAsync(string text) =>
        BroadcastChatAsync(new ChatMessage("PC", text, DateTimeOffset.Now), null);

    private bool IsAuthorized(HttpContext context)
    {
        var supplied = context.Request.Headers["X-LocalDrop-Token"].ToString();
        if (string.IsNullOrEmpty(supplied)) supplied = context.Request.Query["token"].ToString();
        return !string.IsNullOrEmpty(supplied) &&
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(_token));
    }

    private async Task BroadcastChatAsync(ChatMessage message, WebSocket? except)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await _sendLock.WaitAsync();
        try
        {
            foreach (var socket in _sockets.Values)
            {
                if (ReferenceEquals(socket, except) || socket.State != WebSocketState.Open) continue;
                try
                {
                    await socket.SendAsync(new ArraySegment<byte>(payload),
                        WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (WebSocketException) { }
            }
        }
        finally { _sendLock.Release(); }
    }

    private static string GetUniquePath(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        if (!File.Exists(path)) return path;
        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var index = 1;
        do { path = Path.Combine(folder, $"{name} ({index++}){extension}"); }
        while (File.Exists(path));
        return path;
    }

    public async Task StopAsync()
    {
        var app = _app;
        _app = null;
        if (app is not null)
        {
            foreach (var socket in _sockets.Values)
            {
                try
                {
                    if (socket.State == WebSocketState.Open)
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "LocalDrop stopped", CancellationToken.None);
                }
                catch (WebSocketException) { }
            }
            await app.StopAsync();
            await app.DisposeAsync();
        }

        _sockets.Clear();
        _token = "";
        _pairedDeviceId = null;
        _pairedDeviceName = "Waiting for phone";
        DeviceDisconnected?.Invoke();
        StatusChanged?.Invoke("Server stopped");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _sendLock.Dispose();
    }
}
