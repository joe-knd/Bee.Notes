namespace WpfNotes.Core.Models;

/// <summary>
/// Identifies the kind of a <see cref="ChatMessage"/>.
/// </summary>
public enum ChatMessageType
{
    /// <summary>System notification (join confirmation, errors, etc.).</summary>
    System,
    /// <summary>Regular user chat message.</summary>
    Chat,
    /// <summary>A participant joined the room.</summary>
    Join,
    /// <summary>A participant left the room.</summary>
    Leave
}

/// <summary>
/// Represents a single message exchanged over the chat protocol.
/// </summary>
public class ChatMessage
{
    /// <summary>The type of this message.</summary>
    public ChatMessageType Type { get; set; }

    /// <summary>Display name of the sender.</summary>
    public string Sender { get; set; } = string.Empty;

    /// <summary>Message body text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>UTC timestamp of when the message was created.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Hex color string assigned to the sender.</summary>
    public string Color { get; set; } = string.Empty;
}
