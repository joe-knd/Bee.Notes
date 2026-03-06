using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.IO;
using WpfNotes.Core.Models;
using WpfNotes.Core.Services;
using WpfNotes.Features.Editor;
using WpfNotes.Features.Home;

namespace WpfNotes.Features.DocumentHost;

/// <summary>
/// View-model that manages open editor tabs and file operations.
/// </summary>
public partial class DocumentHostViewModel(IServiceProvider provider, INavigationService navigation, INotesService notesService) : ObservableObject
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public ObservableCollection<EditorViewModel> Documents { get; } = new();

    [ObservableProperty]
    private EditorViewModel? _activeDocument;

    [RelayCommand]
    public void Open(Guid? id)
    {
        EditorViewModel vm = id.HasValue
            ? ActivatorUtilities.CreateInstance<EditorViewModel>(provider, (object)id.Value)
            : ActivatorUtilities.CreateInstance<EditorViewModel>(provider);
        Documents.Add(vm);
        ActiveDocument = vm;
    }

    public async Task OpenFile(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("File not found.", fullPath);

        var fileInfo = new FileInfo(fullPath);
        if (fileInfo.Length > MaxFileSizeBytes)
            throw new IOException($"File exceeds the maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB.");

        var vm = ActivatorUtilities.CreateInstance<EditorViewModel>(provider);
        var content = await File.ReadAllTextAsync(fullPath);
        vm.Title = Path.GetFileName(fullPath);
        vm.Content = content;
        vm.FilePath = fullPath;
        Documents.Add(vm);
        ActiveDocument = vm;

        var note = new Note
        {
            Title = vm.Title,
            Content = content,
            FilePath = fullPath,
            LastModified = DateTime.UtcNow
        };
        await notesService.SaveAsync(note);
    }

    [RelayCommand]
    public void Close(EditorViewModel vm)
    {
        Documents.Remove(vm);
        if (Documents.Count > 0)
            ActiveDocument = Documents[^1];
        else
            navigation.Navigate<HomeViewModel>();
    }
}
