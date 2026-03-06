using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WpfNotes.Core.Models;

namespace WpfNotes.Core.Services;

/// <summary>
/// Persists chat session messages to DPAPI-encrypted binary files in local app data.
/// </summary>
public sealed class ChatPersistenceService
{
    private const long MaxTotalSizeBytes = 5L * 1024 * 1024 * 1024; // 5 GB
    private static readonly SemaphoreSlim _lock = new(1, 1);

    private readonly string _chatDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WpfNotes", "chat");

    private readonly List<ChatMessage> _sessionMessages = [];
    private string? _sessionFile;

    public void StartSession()
    {
        Directory.CreateDirectory(_chatDir);
        _sessionFile = Path.Combine(_chatDir, $"session_{DateTime.Now:yyyyMMdd_HHmmss}.bin");
        _sessionMessages.Clear();
        ArchiveIfNeeded();
    }

    public async Task AppendMessageAsync(ChatMessage message)
    {
        if (_sessionFile is null) return;

        _sessionMessages.Add(message);

        await _lock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(_sessionMessages);
            var encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(_sessionFile, encrypted);
        }
        catch { /* best-effort persistence */ }
        finally
        {
            _lock.Release();
        }
    }

    private void ArchiveIfNeeded()
    {
        var dir = new DirectoryInfo(_chatDir);
        if (!dir.Exists) return;

        var files = dir.GetFiles("session_*.bin").OrderBy(f => f.CreationTimeUtc).ToList();
        var totalSize = files.Sum(f => f.Length);
        if (totalSize <= MaxTotalSizeBytes) return;

        var archiveDir = Path.Combine(_chatDir, "archive");
        Directory.CreateDirectory(archiveDir);

        while (totalSize > MaxTotalSizeBytes && files.Count > 1)
        {
            var oldest = files[0];
            files.RemoveAt(0);
            totalSize -= oldest.Length;
            oldest.MoveTo(Path.Combine(archiveDir, oldest.Name), true);
        }
    }
}
