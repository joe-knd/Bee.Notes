using System.Collections.ObjectModel;

namespace WpfNotes.Core.Models;

/// <summary>
/// Groups consecutive chat messages from the same sender for bubble-style display.
/// </summary>
public class ChatMessageGroup
{
    /// <summary>Display name of the sender.</summary>
    public string Sender { get; init; } = string.Empty;

    /// <summary>Hex color string for the sender's bubble.</summary>
    public string Color { get; init; } = string.Empty;

    /// <summary>Whether this group was sent by the local user.</summary>
    public bool IsOwnMessage { get; init; }

    /// <summary>Whether this group contains system messages (join/leave/info).</summary>
    public bool IsSystemMessage { get; init; }

    /// <summary>The messages in this group.</summary>
    public ObservableCollection<ChatMessage> Messages { get; } = [];
}
