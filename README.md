# Bee.Notes

A lightweight Bee! notes application built with .NET 10 and the MVVM pattern.

## Features

- **Tabbed Editor** – Open multiple notes simultaneously in a document-host layout.
- **Auto-Save** – Notes are automatically saved every 10 seconds when changes are detected.
- **DPAPI Encryption** – All persisted notes are encrypted at rest using Windows Data Protection API.
- **File Import** – Open external text files and track them alongside in-app notes.
- **Peer-to-Peer Chat** – Host or join encrypted chat rooms over TCP/TLS with self-signed certificates.
- **Chat Persistence** – Chat sessions are persisted locally in encrypted binary files.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| UI | WPF (XAML) |
| Framework | .NET 10 |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| Encryption | DPAPI (`ProtectedData`) |
| Networking | TCP + SslStream (TLS 1.2/1.3) |

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows (required for WPF and DPAPI)

### Build & Run

```bash
dotnet build
dotnet run --project Bee.Notes
```

## Project Structure

```
Bee.Notes/
├── Core/
│   ├── Models/          # Domain models (Note, ChatMessage, ChatMessageGroup)
│   └── Services/        # Business logic and data access
├── Features/
│   ├── Chat/            # Peer-to-peer chat view & view-model
│   ├── DocumentHost/    # Tabbed document container
│   ├── Editor/          # Note editor view & view-model
│   └── Home/            # Home screen with recent notes
├── Navigation/          # Navigation store for MVVM routing
├── App.xaml(.cs)        # Application entry point & DI configuration
└── MainWindow.xaml(.cs) # Shell window with sidebar navigation
```

## License

This project is provided as-is for personal and educational use.
