using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FireFenyx.Notifications.Services;
using System;
using System.IO;
using System.Windows.Threading;
using WpfNotes.Core.Models;
using WpfNotes.Core.Services;

namespace WpfNotes.Features.Editor;

/// <summary>
/// View-model for a single note editor tab with auto-save support.
/// </summary>
public partial class EditorViewModel : ObservableObject
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    private readonly INotesService _notesService;
    private readonly INotificationService _notifications;

    private Guid? _id;

    private string _initialTitle = "";
    private string _initialContent = "";

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _content = string.Empty;

    public string? FilePath { get; set; }

    public bool IsDirty => Title != _initialTitle || Content != _initialContent;

    private readonly DispatcherTimer _autosaveTimer = new() { Interval = TimeSpan.FromSeconds(10) };

    public EditorViewModel(INotesService notesService, INotificationService notifications, object? parameter = null)
    {
        _notesService = notesService;
        _notifications = notifications;

        if (parameter is Guid id)
        {
            _id = id;
            _ = LoadAsync(id);
        }

        _autosaveTimer.Tick += async (_, _) => await AutoSaveAsync();
        _autosaveTimer.Start();
    }

    private async Task LoadAsync(Guid id)
    {
        var note = await _notesService.GetAsync(id);
        if (note is null) return;

        if (note.FilePath is not null && File.Exists(note.FilePath))
        {
            var fullPath = Path.GetFullPath(note.FilePath);
            if (File.Exists(fullPath) && new FileInfo(fullPath).Length <= MaxFileSizeBytes)
            {
                FilePath = fullPath;
                Title = Path.GetFileName(fullPath);
                Content = await File.ReadAllTextAsync(fullPath);
                _notifications.Info($"Loaded content from {fullPath}");
            }
            else
            {
                Title = note.Title;
                Content = note.Content;
                _notifications.Warning($"File {fullPath} is too large to load or does not exist. Loaded note content from database instead.");
            }
        }
        else
        {
            Title = note.Title;
            Content = note.Content;
            _notifications.Info($"Loaded note content from database.");
        }

        _initialTitle = Title;
        _initialContent = Content;
        _notifications.Success($"Note loaded successfully.");
    }

    private async Task AutoSaveAsync()
    {
        if (!IsDirty) return;

        await SaveInternalAsync();
        _notifications.Info($"Note auto-saved at {DateTime.Now:T}");
    }

    private async Task SaveInternalAsync()
    {
        if (FilePath is not null)
        {
            await File.WriteAllTextAsync(FilePath, Content);
            _notifications.Info($"Content saved to {FilePath}");
        }

        var note = new Note
        {
            Id = _id ?? Guid.NewGuid(),
            Title = Title,
            Content = Content,
            FilePath = FilePath,
            LastModified = DateTime.UtcNow
        };

        await _notesService.SaveAsync(note);
        _id = note.Id;

        _initialTitle = Title;
        _initialContent = Content;
        _notifications.Success($"Note saved successfully.");
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await SaveInternalAsync();
        _notifications.Success($"Note saved successfully.");
    }
}
