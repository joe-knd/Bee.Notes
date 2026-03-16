using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FireFenyx.Notifications.Services;
using System;
using System.Collections.ObjectModel;
using WpfNotes.Core.Models;
using WpfNotes.Core.Services;
using WpfNotes.Features.DocumentHost;

namespace WpfNotes.Features.Home;

/// <summary>
/// View-model for the home screen, displaying recent notes and providing create/open/delete actions.
/// </summary>
public partial class HomeViewModel : ObservableObject
{
    private readonly INotesService _notesService;
    private readonly INavigationService _navigation;
    private readonly DocumentHostViewModel _host;
    private readonly INotificationService _notifications;

    public ObservableCollection<Note> RecentNotes { get; } = new();

    public HomeViewModel(INotesService notesService, INavigationService navigation, INotificationService notifications, DocumentHostViewModel host)
    {
        _notesService = notesService;
        _navigation = navigation;
        _host = host;
        _notifications = notifications;

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var notes = await _notesService.GetRecentAsync();
        RecentNotes.Clear();
        foreach (var note in notes)
            RecentNotes.Add(note);
        _notifications.Success($"Loaded {notes.Count()} recent notes.");
    }

    [RelayCommand]
    private void NewNote()
    {
        _host.Open(null);
        _navigation.Navigate<DocumentHostViewModel>();
        _notifications.Info("Creating new note...");
    }

    [RelayCommand]
    private void OpenNote(Note note)
    {
        _host.Open(note.Id);
        _navigation.Navigate<DocumentHostViewModel>();
        _notifications.Info($"Opening note {note.Title}...");
    }

    [RelayCommand]
    private async Task DeleteNote(Note note)
    {
        await _notesService.DeleteAsync(note.Id);
        RecentNotes.Remove(note);
        _notifications.Warning($"Deleted note {note.Title}.");
    }
}
