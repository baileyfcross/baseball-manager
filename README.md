# BaseballManager

BaseballManager is a C# + .NET 9 + MonoGame DesktopGL project.

- Targets: Windows, macOS, Linux
- Development environment: Visual Studio Code on macOS and Windows 10
- Game type: 2D baseball manager with live match gameplay as the core experience

## Solution Organization

- `src/BaseballManager.Core`: what the world is (domain model)
- `src/BaseballManager.Sim`: how baseball behaves (simulation rules and resolution)
- `src/BaseballManager.Application`: what the user can do (use cases)
- `src/BaseballManager.Infrastructure`: how the game stores and loads (persistence and integrations)
- `src/BaseballManager.Contracts`: cross-boundary DTO contracts
- `src/BaseballManager.Game`: how everything is presented and controlled (MonoGame DesktopGL executable)

MonoGame-specific code is isolated to `BaseballManager.Game`.

## Build And Run

### VS Code

- Build task: `build-game`
- Run task: `run-game`
- Debug config: `Launch BaseballManager.Game`

### Command Line

```bash
dotnet build BaseballManager.sln
dotnet run --project src/BaseballManager.Game/BaseballManager.Game.csproj
```

## Platforms

The project is set up around one MonoGame DesktopGL executable plus shared class libraries for core domain, simulation, application logic, infrastructure, and contracts.
