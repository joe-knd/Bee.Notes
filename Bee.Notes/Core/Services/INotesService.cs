using WpfNotes.Core.Models;

namespace WpfNotes.Core.Services;

/// <summary>
/// Provides CRUD operations for user notes.
/// </summary>
public interface INotesService
{
    /// <summary>Get the most recent notes.</summary>
    Task<IEnumerable<Note>> GetRecentAsync();

    /// <summary>Get a single note by its unique identifier.</summary>
    /// <param name="id">The note identifier.</param>
    Task<Note?> GetAsync(Guid id);

    /// <summary>Create or update a note.</summary>
    /// <param name="note">The note to save.</param>
    Task SaveAsync(Note note);

    /// <summary>Delete a note by its unique identifier.</summary>
    /// <param name="id">The note identifier.</param>
    Task DeleteAsync(Guid id);
}