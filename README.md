# AvalonHttp

AvalonHttp is a cross-platform desktop HTTP client application built with [Avalonia UI](https://avaloniaui.net/) and .NET. 
It provides a lightweight and fast interface to create, manage, and execute HTTP requests, organize them into collections, and manage environment variables.

## Features

- **Collections Workspace:** Organize your HTTP requests into collections for easy access and execution.
- **Environments:** Manage different environments (e.g., Development, Staging, Production) and reuse variables across your requests.
- **Modern UI:** A clean, responsive, and fluent interface powered by Avalonia UI's cross-platform capabilities.
- **Save & Manage Requests:** Quickly save and recall requests (Ctrl+S shortcut included).
- **Cross-Platform:** Runs seamlessly on Windows, macOS, and Linux.

## Technologies

- [C# / .NET](https://dotnet.microsoft.com/) - Target framework configured for modern .NET.
- [Avalonia UI](https://github.com/AvaloniaUI/Avalonia) (v11.3) - Cross-platform UI framework.
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - For Model-View-ViewModel architecture.
- [Serilog](https://serilog.net/) - For structured logging.
- [LucideAvalonia](https://lucide.dev/) & [FluentIcons.Avalonia](https://github.com/davidebianchi/FluentIcons.Avalonia) - For rich iconography.

## Getting Started

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (Version 10.0 or compatible as indicated by the project's target framework).

### Building and Running

1. **Clone the repository:**
   ```bash
   git clone <repository-url>
   cd AvalonHttp
   ```

2. **Restore dependencies and build the project:**
   ```bash
   dotnet build
   ```

3. **Run the application:**
   You can run the application directly using the `dotnet run` command:
   ```bash
   dotnet run --project src/AvalonHttp.csproj
   ```

## Project Structure

- `src/` - Contains the main application source code (Models, Views, ViewModels, Services).
- `tests/` - Contains the unit and integration tests for the project.
- `logs/` - Automatically generated execution logs (via Serilog).

## License

This project is licensed under the terms specified in the `LICENSE` file.
