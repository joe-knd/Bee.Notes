using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using WpfNotes.Core.Models;

namespace WpfNotes.Core.Services;

/// <summary>
/// TCP/TLS-based peer-to-peer chat service that can act as either host or client.
/// </summary>
public sealed class ChatService : IChatService, IDisposable
{
    private const int MaxMessageBytes = 65_536;
    private const int MaxTextLength = 4_096;
    private const int RateLimitPerSecond = 10;

    /// <summary>Default TCP port used for chat connections.</summary>
    public const int DefaultPort = 9600;

    private static readonly JsonSerializerOptions s_indentedJsonOptions = new() { WriteIndented = true };

    private static readonly string[] Palette =
    [
        "#FF5722", "#E91E63", "#9C27B0", "#673AB7",
        "#3F51B5", "#2196F3", "#009688", "#4CAF50",
        "#8BC34A", "#FF9800", "#795548", "#607D8B"
    ];

    private static readonly string TrustedFingerprintsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WpfNotes", "trusted_hosts.json");

    private readonly object _lock = new();
    private readonly List<RemoteClient> _remoteClients = [];
    private TcpListener? _listener;
    private TcpClient? _tcpClient;
    private SslStream? _serverStream;
    private SemaphoreSlim? _serverWriteLock;
    private X509Certificate2? _cert;
    private CancellationTokenSource? _cts;
    private string? _roomPasswordHash;
    private int _nextColor;

    public event Action<ChatMessage>? MessageReceived;
    public event Action<string>? StatusChanged;

    public bool IsConnected { get; private set; }
    public bool IsHosting { get; private set; }
    public string? DisplayName { get; private set; }
    public string? OwnColor { get; private set; }
    public string? CertificateFingerprint => _cert?.GetCertHashString(HashAlgorithmName.SHA256);

    private string NextColor()
    {
        var color = Palette[_nextColor % Palette.Length];
        _nextColor++;
        return color;
    }

    // ═══════════════════════════════════════════════════════
    // HOST
    // ═══════════════════════════════════════════════════════

    public Task HostAsync(string displayName, int port, string roomPassword)
    {
        if (IsConnected) return Task.CompletedTask;

        _cts = new CancellationTokenSource();
        _cert = GenerateSelfSignedCert();
        _roomPasswordHash = HashPassword(roomPassword);
        DisplayName = displayName;
        OwnColor = NextColor();
        IsHosting = true;
        IsConnected = true;

        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();

        StatusChanged?.Invoke($"Hosting on port {port} | Fingerprint: {CertificateFingerprint}");

        RaiseMessage(new ChatMessage
        {
            Type = ChatMessageType.System,
            Sender = displayName,
            Text = $"{displayName} started the chat",
            Timestamp = DateTime.UtcNow,
            Color = OwnColor
        });

        _ = AcceptLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var tcp = await _listener!.AcceptTcpClientAsync(ct);
                _ = HandleRemoteClientAsync(tcp, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    private async Task HandleRemoteClientAsync(TcpClient tcp, CancellationToken ct)
    {
        SslStream? ssl = null;
        RemoteClient? remote = null;
        try
        {
            ssl = new SslStream(tcp.GetStream(), false);
            await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = _cert,
                ClientCertificateRequired = false,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }, ct);

            var join = await ReadMessageAsync(ssl, ct);
            if (join is null || join.Type != ChatMessageType.Join || string.IsNullOrWhiteSpace(join.Sender))
            {
                ssl.Dispose();
                tcp.Dispose();
                return;
            }

            // Verify room password (hash sent by client must match server's hash)
            if (_roomPasswordHash is not null && join.Color != _roomPasswordHash)
            {
                await WriteMessageAsync(ssl, new SemaphoreSlim(1, 1), new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Sender = "Server",
                    Text = "AUTH_FAIL",
                    Timestamp = DateTime.UtcNow
                }, ct);
                ssl.Dispose();
                tcp.Dispose();
                return;
            }

            var color = NextColor();
            remote = new RemoteClient(join.Sender, color, ssl, tcp, new SemaphoreSlim(1, 1));
            lock (_lock) _remoteClients.Add(remote);

            // Tell the new client their assigned color
            await WriteMessageAsync(ssl, remote.WriteLock, new ChatMessage
            {
                Type = ChatMessageType.System,
                Sender = "Server",
                Text = $"COLOR:{color}",
                Timestamp = DateTime.UtcNow,
                Color = color
            }, ct);

            var joinMsg = new ChatMessage
            {
                Type = ChatMessageType.Join,
                Sender = join.Sender,
                Text = $"{join.Sender} joined",
                Timestamp = DateTime.UtcNow,
                Color = color
            };
            await BroadcastAsync(joinMsg, ct);
            RaiseMessage(joinMsg);

            // Read loop
            int messageCount = 0;
            var windowStart = DateTime.UtcNow;
            while (!ct.IsCancellationRequested)
            {
                var msg = await ReadMessageAsync(ssl, ct);
                if (msg is null) break;

                // Rate limiting
                var now = DateTime.UtcNow;
                if ((now - windowStart).TotalSeconds >= 1)
                {
                    messageCount = 0;
                    windowStart = now;
                }
                messageCount++;
                if (messageCount > RateLimitPerSecond) break; // disconnect flooder

                msg.Color = color;
                msg.Sender = remote.Name; // prevent sender spoofing
                msg.Timestamp = DateTime.UtcNow;

                if (msg.Type == ChatMessageType.Chat)
                {
                    if (msg.Text.Length > MaxTextLength)
                        msg.Text = msg.Text[..MaxTextLength];

                    await BroadcastAsync(msg, ct, exclude: remote);
                    RaiseMessage(msg);
                }
            }
        }
        catch { /* client disconnected */ }
        finally
        {
            if (remote is not null)
            {
                lock (_lock) _remoteClients.Remove(remote);
                var leaveMsg = new ChatMessage
                {
                    Type = ChatMessageType.Leave,
                    Sender = remote.Name,
                    Text = $"{remote.Name} left",
                    Timestamp = DateTime.UtcNow,
                    Color = remote.Color
                };
                try { await BroadcastAsync(leaveMsg, default); } catch { }
                RaiseMessage(leaveMsg);
                remote.Dispose();
            }
            else
            {
                ssl?.Dispose();
                tcp.Dispose();
            }
        }
    }

    private async Task BroadcastAsync(ChatMessage msg, CancellationToken ct, RemoteClient? exclude = null)
    {
        List<RemoteClient> snapshot;
        lock (_lock) snapshot = [.. _remoteClients];

        foreach (var c in snapshot)
        {
            if (c == exclude) continue;
            try { await WriteMessageAsync(c.Stream, c.WriteLock, msg, ct); }
            catch { /* will be cleaned up when read loop fails */ }
        }
    }

    // ═══════════════════════════════════════════════════════
    // JOIN (CLIENT)
    // ═══════════════════════════════════════════════════════

    public async Task JoinAsync(string displayName, string host, int port, string roomPassword)
    {
        if (IsConnected) return;

        _cts = new CancellationTokenSource();
        DisplayName = displayName;
        IsHosting = false;

        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(host, port, _cts.Token);

        X509Certificate2? serverCert = null;
        var hostKey = $"{host}:{port}";
        var ssl = new SslStream(_tcpClient.GetStream(), false, (_, cert, _, _) =>
        {
            if (cert is null) return false;
            serverCert = new X509Certificate2(cert);
            var fingerprint = serverCert.GetCertHashString(HashAlgorithmName.SHA256);

            // Save / update fingerprint (TOFU — trust on first use per session)
            var trusted = LoadTrustedFingerprints();
            trusted[hostKey] = fingerprint;
            SaveTrustedFingerprints(trusted);
            return true;
        });

        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = host,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
        }, _cts.Token);

        _cert = serverCert;
        _serverStream = ssl;
        _serverWriteLock = new SemaphoreSlim(1, 1);
        IsConnected = true;

        StatusChanged?.Invoke($"Connected to {host}:{port} | Fingerprint: {CertificateFingerprint}");

        // Send Join with password hash
        await WriteMessageAsync(ssl, _serverWriteLock, new ChatMessage
        {
            Type = ChatMessageType.Join,
            Sender = displayName,
            Color = HashPassword(roomPassword), // piggyback password hash in Color field
            Timestamp = DateTime.UtcNow
        }, _cts.Token);

        _ = ClientReadLoopAsync(ssl, _cts.Token);
    }

    private async Task ClientReadLoopAsync(SslStream ssl, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var msg = await ReadMessageAsync(ssl, ct);
                if (msg is null) break;

                // Server sends COLOR:<hex> to assign our bubble color
                if (msg.Type == ChatMessageType.System && msg.Text.StartsWith("COLOR:", StringComparison.Ordinal))
                {
                    OwnColor = msg.Text[6..];
                    continue;
                }

                // Server rejected our password
                if (msg.Type == ChatMessageType.System && msg.Text == "AUTH_FAIL")
                {
                    IsConnected = false;
                    StatusChanged?.Invoke("Authentication failed — wrong room password");
                    break;
                }

                RaiseMessage(msg);
            }
        }
        catch { /* disconnected */ }
        finally
        {
            IsConnected = false;
            StatusChanged?.Invoke("Disconnected");
        }
    }

    // ═══════════════════════════════════════════════════════
    // SEND
    // ═══════════════════════════════════════════════════════

    public async Task SendAsync(string text)
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(text)) return;

        if (text.Length > MaxTextLength)
            text = text[..MaxTextLength];

        var msg = new ChatMessage
        {
            Type = ChatMessageType.Chat,
            Sender = DisplayName ?? "Unknown",
            Text = text,
            Timestamp = DateTime.UtcNow,
            Color = OwnColor ?? ""
        };

        if (IsHosting)
        {
            await BroadcastAsync(msg, _cts?.Token ?? default);
            RaiseMessage(msg);
        }
        else if (_serverStream is not null && _serverWriteLock is not null)
        {
            await WriteMessageAsync(_serverStream, _serverWriteLock, msg, _cts?.Token ?? default);
            RaiseMessage(msg); // display locally immediately
        }
    }

    // ═══════════════════════════════════════════════════════
    // DISCONNECT
    // ═══════════════════════════════════════════════════════

    public Task DisconnectAsync()
    {
        if (!IsConnected) return Task.CompletedTask;

        try { _cts?.Cancel(); } catch { }

        if (IsHosting)
        {
            try { _listener?.Stop(); } catch { }
            List<RemoteClient> snapshot;
            lock (_lock) snapshot = [.. _remoteClients];
            foreach (var c in snapshot) c.Dispose();
            lock (_lock) _remoteClients.Clear();
        }
        else
        {
            try { _serverStream?.Dispose(); } catch { }
            try { _serverWriteLock?.Dispose(); } catch { }
            try { _tcpClient?.Dispose(); } catch { }
        }

        IsConnected = false;
        IsHosting = false;
        _serverStream = null;
        _serverWriteLock = null;
        _tcpClient = null;
        _cert = null;
        _roomPasswordHash = null;
        _cts?.Dispose();
        _cts = null;

        StatusChanged?.Invoke("Disconnected");
        return Task.CompletedTask;
    }

    // ═══════════════════════════════════════════════════════
    // WIRE PROTOCOL (length-prefixed JSON over TLS)
    // ═══════════════════════════════════════════════════════

    private static async Task<ChatMessage?> ReadMessageAsync(SslStream stream, CancellationToken ct)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, ct);
        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length <= 0 || length > MaxMessageBytes) return null;

        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, ct);
        return JsonSerializer.Deserialize<ChatMessage>(Encoding.UTF8.GetString(payload));
    }

    private static async Task WriteMessageAsync(SslStream stream, SemaphoreSlim writeLock, ChatMessage message, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(message);
        var payload = Encoding.UTF8.GetBytes(json);
        if (payload.Length > MaxMessageBytes) return;

        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);

        await writeLock.WaitAsync(ct);
        try
        {
            await stream.WriteAsync(header, ct);
            await stream.WriteAsync(payload, ct);
            await stream.FlushAsync(ct);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private void RaiseMessage(ChatMessage msg) => MessageReceived?.Invoke(msg);

    // ═══════════════════════════════════════════════════════
    // CERTIFICATE
    // ═══════════════════════════════════════════════════════

    private static X509Certificate2 GenerateSelfSignedCert()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            "CN=WpfNotesChat",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null);
    }

    // ═══════════════════════════════════════════════════════
    // PASSWORD HASHING (SHA-256, not stored — only compared)
    // ═══════════════════════════════════════════════════════

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexStringLower(bytes);
    }

    // ═══════════════════════════════════════════════════════
    // TRUSTED FINGERPRINT STORE (TOFU pinning)
    // ═══════════════════════════════════════════════════════

    private static Dictionary<string, string> LoadTrustedFingerprints()
    {
        try
        {
            if (!File.Exists(TrustedFingerprintsPath))
                return new Dictionary<string, string>();
            var json = File.ReadAllText(TrustedFingerprintsPath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch { return new Dictionary<string, string>(); }
    }

    private static void SaveTrustedFingerprints(Dictionary<string, string> store)
    {
        try
        {
            var dir = Path.GetDirectoryName(TrustedFingerprintsPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(store, s_indentedJsonOptions);
            File.WriteAllText(TrustedFingerprintsPath, json);
        }
        catch { /* best-effort */ }
    }

    // ═══════════════════════════════════════════════════════
    // INNER TYPES
    // ═══════════════════════════════════════════════════════

    /// <inheritdoc />
    public void Dispose()
    {
        if (IsConnected)
        {
            try { _cts?.Cancel(); } catch { }

            if (IsHosting)
            {
                try { _listener?.Stop(); } catch { }
                lock (_lock)
                {
                    foreach (var c in _remoteClients) c.Dispose();
                    _remoteClients.Clear();
                }
            }
        }

        _serverStream?.Dispose();
        _serverWriteLock?.Dispose();
        _tcpClient?.Dispose();
        _cts?.Dispose();
        _cert?.Dispose();

        IsConnected = false;
        IsHosting = false;
    }

    private sealed class RemoteClient(string name, string color, SslStream stream, TcpClient tcp, SemaphoreSlim writeLock) : IDisposable
    {
        public string Name => name;
        public string Color => color;
        public SslStream Stream => stream;
        public SemaphoreSlim WriteLock => writeLock;

        public void Dispose()
        {
            try { stream.Dispose(); } catch { }
            try { tcp.Dispose(); } catch { }
            try { writeLock.Dispose(); } catch { }
        }
    }
}
