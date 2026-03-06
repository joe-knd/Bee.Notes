namespace WpfNotes.Core.Models;

/// <summary>
/// Represents a user note with optional file-backed storage.
/// </summary>
public class Note
{
    /// <summary>Unique identifier for the note.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Title of the note.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Full text content of the note.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>UTC timestamp of the last modification.</summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    /// <summary>Optional file system path when the note is backed by a file.</summary>
    public string? FilePath { get; set; }

    /// <summary>Short preview of the content (up to 200 characters).</summary>
    public string Preview =>
        Content.Length > 200 ? Content[..200] + "..." : Content;
}
