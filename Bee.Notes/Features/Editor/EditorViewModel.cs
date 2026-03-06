using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public EditorViewModel(INotesService notesService, object? parameter = null)
    {
        _notesService = notesService;

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
            }
            else
            {
                Title = note.Title;
                Content = note.Content;
            }
        }
        else
        {
            Title = note.Title;
            Content = note.Content;
        }

        _initialTitle = Title;
        _initialContent = Content;
    }

    private async Task AutoSaveAsync()
    {
        if (!IsDirty) return;

        await SaveInternalAsync();
    }

    private async Task SaveInternalAsync()
    {
        if (FilePath is not null)
        {
            await File.WriteAllTextAsync(FilePath, Content);
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
    }

    [RelayCommand]
    private async Task SaveAsync() =>
        await SaveInternalAsync();
}
