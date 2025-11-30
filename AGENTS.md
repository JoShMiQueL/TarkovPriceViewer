# AGENTS.md

## Build Commands
- **Build**: `dotnet build` - Compiles the project
- **Run**: `dotnet run` - Builds and runs the application  
- **Publish**: `dotnet publish -c Release` - Creates release build in publish/ folder
- **Clean**: `dotnet clean` - Cleans build artifacts

## Code Style Guidelines

### Imports & Namespaces
- Group System.* imports first, then third-party, then local namespaces
- Use `using static` only for frequently accessed static members
- Keep imports at top of files, alphabetized within groups

### Naming Conventions  
- **Classes**: PascalCase (e.g., `SettingsService`, `MainForm`)
- **Interfaces**: PascalCase with `I` prefix (e.g., `ISettingsService`)
- **Methods**: PascalCase (e.g., `LoadBallisticsAsync`)
- **Properties**: PascalCase (e.g., `Settings`, `IsLoaded`)
- **Fields**: camelCase with underscore prefix for private fields (e.g., `_settingsService`)
- **Constants**: PascalCase or UPPER_CASE (e.g., `SETTING_PATH`, `AppSettings`)

### Error Handling
- Use try-catch blocks for external API calls and file I/O
- Log errors using `Debug.WriteLine()` with descriptive messages
- Never let exceptions bubble up to UI layer without handling
- Use null-coalescing operator (`??`) for fallback values

### Types & Patterns
- Prefer dependency injection with constructor parameters
- Use async/await for I/O operations and API calls
- Implement interfaces for services and testability
- Use `var` only when type is obvious from right side

### Formatting
- 4-space indentation (no tabs)
- Opening braces on same line for methods/classes
- Use regions for organizing large files (e.g., #region "Constructor")
- Keep lines under 120 characters when possible

### Architecture
- **Runtime**: .NET `net10.0-windows` (WPF)
- **Services**: `/Services/` folder with corresponding interfaces
- **Models**: `/Models/` folder for data structures  
- **UI (WPF)**: `/UI/` folder for WPF windows, views and controls (`MainWindow`, `OverlayWindow`, etc.)
- **Configuration**: `/Configuration/` folder for settings, options and configuration bindings
- **Utils**: `/Utils/` folder for shared utilities