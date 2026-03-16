using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FireFenyx.Notifications.Services;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using WpfNotes.Core.Models;
using WpfNotes.Core.Services;

namespace WpfNotes.Features.Chat;

/// <summary>
/// View-model for the peer-to-peer chat feature, managing connection state and message display.
/// </summary>
public partial class ChatViewModel : ObservableObject
{
    private readonly IChatService _chat;
    private readonly ChatPersistenceService _persistence;
    private readonly Dispatcher _dispatcher;
    private readonly INotificationService _notifications;

    public ObservableCollection<ChatMessageGroup> MessageGroups { get; } = [];

    [ObservableProperty] private string _displayName = Environment.UserName;
    [ObservableProperty] private string _hostAddress = "127.0.0.1";
    [ObservableProperty] private int _port = ChatService.DefaultPort;
    [ObservableProperty] private string _messageText = string.Empty;
    [ObservableProperty] private string _roomPassword = string.Empty;
    [ObservableProperty] private string _status = "Disconnected";
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _fingerprint = string.Empty;

    public event Action? ScrollRequested;

    public ChatViewModel(IChatService chat, ChatPersistenceService persistence, INotificationService notifications)
    {
        _chat = chat;
        _persistence = persistence;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _notifications = notifications;

        _chat.MessageReceived += OnMessageReceived;
        _chat.StatusChanged += OnChatStatusChanged;
    }

    private void OnMessageReceived(ChatMessage msg)
    {
        _dispatcher.BeginInvoke(() =>
        {
            AddMessage(msg);
            _ = _persistence.AppendMessageAsync(msg);
        });
    }

    private void OnChatStatusChanged(string status)
    {
        _dispatcher.BeginInvoke(() =>
        {
            Status = status;
            IsConnected = _chat.IsConnected;
            Fingerprint = _chat.CertificateFingerprint ?? string.Empty;
        });

        _notifications.Info($"Chat status changed: {status}");
    }

    private void AddMessage(ChatMessage msg)
    {
        bool isOwn = msg.Sender == _chat.DisplayName;
        bool isSystem = msg.Type is ChatMessageType.Join or ChatMessageType.Leave or ChatMessageType.System;

        // Group consecutive messages from the same non-system sender
        if (!isSystem && MessageGroups.Count > 0)
        {
            var last = MessageGroups[^1];
            if (!last.IsSystemMessage && last.Sender == msg.Sender)
            {
                last.Messages.Add(msg);
                ScrollRequested?.Invoke();
                return;
            }
        }

        var group = new ChatMessageGroup
        {
            Sender = msg.Sender,
            Color = isSystem ? string.Empty : msg.Color,
            IsOwnMessage = isOwn,
            IsSystemMessage = isSystem
        };
        group.Messages.Add(msg);
        MessageGroups.Add(group);
        ScrollRequested?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task HostAsync()
    {
        try
        {
            _persistence.StartSession();
            await _chat.HostAsync(DisplayName, Port, RoomPassword);
            IsConnected = _chat.IsConnected;
            Fingerprint = _chat.CertificateFingerprint ?? string.Empty;
            _notifications.Success("Hosting started successfully");
        }
        catch (Exception ex)
        {
            Status = $"Host failed: {ex.Message}";
            _notifications.Error($"Hosting failed: {ex.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task JoinAsync()
    {
        try
        {
            _persistence.StartSession();
            await _chat.JoinAsync(DisplayName, HostAddress, Port, RoomPassword);
            IsConnected = _chat.IsConnected;
            Fingerprint = _chat.CertificateFingerprint ?? string.Empty;
            _notifications.Success("Joined chat successfully");
        }
        catch (Exception ex)
        {
            Status = $"Join failed: {ex.Message}";
            _notifications.Error($"Join failed: {ex.Message}");
        }
    }

    private bool CanConnect() => !IsConnected;

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(MessageText) || !IsConnected) return;
        await _chat.SendAsync(MessageText);
        MessageText = string.Empty;
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await _chat.DisconnectAsync();
        IsConnected = false;
        Status = "Disconnected";
        _notifications.Info("Disconnected from chat");
    }

    partial void OnIsConnectedChanged(bool value)
    {
        HostCommand.NotifyCanExecuteChanged();
        JoinCommand.NotifyCanExecuteChanged();
    }
}
