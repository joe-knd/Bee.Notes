using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WpfNotes.Core.Models;

namespace WpfNotes.Core.Services;

/// <summary>
/// File-backed <see cref="INotesService"/> that stores notes encrypted with DPAPI.
/// </summary>
public class NotesService : INotesService
{
    private static readonly SemaphoreSlim _fileLock = new(1, 1);

    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WpfNotes", "notes.json");

    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { WriteIndented = true };

    public async Task<IEnumerable<Note>> GetRecentAsync()
    {
        if (!File.Exists(_path))
            return Enumerable.Empty<Note>();

        await _fileLock.WaitAsync();
        try
        {
            var encrypted = await File.ReadAllBytesAsync(_path);
            var json = Unprotect(encrypted);
            var notes = JsonSerializer.Deserialize<List<Note>>(json) ?? [];
            return notes.OrderByDescending(n => n.LastModified).Take(10);
        }
        catch (JsonException)
        {
            return Enumerable.Empty<Note>();
        }
        catch (CryptographicException)
        {
            return Enumerable.Empty<Note>();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<Note?> GetAsync(Guid id)
    {
        var notes = (await GetRecentAsync()).ToList();
        return notes.FirstOrDefault(n => n.Id == id);
    }

    public async Task SaveAsync(Note note)
    {
        await _fileLock.WaitAsync();
        try
        {
            var notes = await ReadAllNotesUnsafeAsync();

            var existing = note.FilePath is not null
                ? notes.FirstOrDefault(n => n.FilePath == note.FilePath)
                : notes.FirstOrDefault(n => n.Id == note.Id);

            if (existing != null)
            {
                note.Id = existing.Id;
                notes.Remove(existing);
            }

            notes.Add(note);

            await WriteAllNotesUnsafeAsync(notes);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        await _fileLock.WaitAsync();
        try
        {
            var notes = await ReadAllNotesUnsafeAsync();
            var existing = notes.FirstOrDefault(n => n.Id == id);
            if (existing != null)
            {
                notes.Remove(existing);
                await WriteAllNotesUnsafeAsync(notes);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Reads notes without acquiring the lock. Caller must hold <see cref="_fileLock"/>.
    /// </summary>
    private async Task<List<Note>> ReadAllNotesUnsafeAsync()
    {
        if (!File.Exists(_path))
            return [];

        try
        {
            var encrypted = await File.ReadAllBytesAsync(_path);
            var json = Unprotect(encrypted);
            return JsonSerializer.Deserialize<List<Note>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (CryptographicException)
        {
            return [];
        }
    }

    private async Task WriteAllNotesUnsafeAsync(List<Note> notes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var json = JsonSerializer.Serialize(notes, _jsonOptions);
        var encrypted = Protect(json);
        await File.WriteAllBytesAsync(_path, encrypted);
    }

    private static byte[] Protect(string json)
    {
        var plainBytes = Encoding.UTF8.GetBytes(json);
        return ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
    }

    private static string Unprotect(byte[] encrypted)
    {
        var plainBytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plainBytes);
    }
}