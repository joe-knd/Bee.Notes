using WpfNotes.Core.Models;

namespace WpfNotes.Core.Services;

/// <summary>
/// Abstraction for peer-to-peer encrypted chat operations.
/// </summary>
public interface IChatService
{
    /// <summary>Raised when a new chat message arrives.</summary>
    event Action<ChatMessage>? MessageReceived;

    /// <summary>Raised when the connection status text changes.</summary>
    event Action<string>? StatusChanged;

    /// <summary>Indicates whether a connection is currently active.</summary>
    bool IsConnected { get; }

    /// <summary>Indicates whether this instance is acting as the host.</summary>
    bool IsHosting { get; }

    /// <summary>The display name used in the current session.</summary>
    string? DisplayName { get; }

    /// <summary>The color assigned to the local user for message bubbles.</summary>
    string? OwnColor { get; }

    /// <summary>SHA-256 fingerprint of the TLS certificate used by the session.</summary>
    string? CertificateFingerprint { get; }

    /// <summary>Start hosting a chat room on the specified <paramref name="port"/>.</summary>
    /// <param name="displayName">Name shown to other participants.</param>
    /// <param name="port">TCP port to listen on.</param>
    /// <param name="roomPassword">Password required to join the room.</param>
    Task HostAsync(string displayName, int port, string roomPassword);

    /// <summary>Join an existing chat room.</summary>
    /// <param name="displayName">Name shown to other participants.</param>
    /// <param name="host">Hostname or IP address of the host.</param>
    /// <param name="port">TCP port of the host.</param>
    /// <param name="roomPassword">Room password.</param>
    Task JoinAsync(string displayName, string host, int port, string roomPassword);

    /// <summary>Send a chat message to all participants.</summary>
    /// <param name="text">Message text to send.</param>
    Task SendAsync(string text);

    /// <summary>Disconnect from the current session.</summary>
    Task DisconnectAsync();
}
