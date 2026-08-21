## Project

Muno is a Windows desktop application for local audio mastering.

- C# / .NET 10
- WinUI 3 / Windows App SDK
- CommunityToolkit.Mvvm
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Logging with Serilog
- `phase_limiter.exe` is used as the mastering engine.

The application is currently a single project.

## Architecture

- Use MVVM.
- Views contain UI-specific behavior only.
- ViewModels contain UI state and commands.
- Services contain application logic and integrations.
- Use dependency injection.
- Keep PhaseLimiter integration isolated in a dedicated service.
- Prefer simple implementations. Do not introduce additional projects or architectural layers without a concrete requirement.

## Service Contracts

- All public service methods return `Result`, `Result<T>`, `Task<Result>`, or `Task<Result<T>>`.
- Expected operational failures are returned as failed Results.
- ViewModels must always check Results before continuing the workflow or using returned data.
- User-relevant failures must be presented by the ViewModel as an appropriate UI error.
- Exceptions may be used internally but must be converted to Results at public service boundaries.

## PhaseLimiter

- Treat `phase_limiter.exe` as an external CLI mastering engine.
- Keep process execution, CLI arguments, stdout/stderr parsing, progress, exit codes, and cancellation inside the PhaseLimiter integration service.
- Do not expose raw PhaseLimiter CLI arguments to Views or ViewModels.
- Represent mastering settings with strongly typed C# models.
- Use `ProcessStartInfo.ArgumentList` for command-line arguments.
- Never modify the original input audio file.

## MVVM and UI

- Use CommunityToolkit.Mvvm.
- Prefer `[ObservableProperty]`, `[RelayCommand]`, and `[AsyncRelayCommand]`.
- Keep business logic out of ViewModels, Views and code-behind.
- Code-behind is allowed for purely view-specific behavior.
- Keep the UI responsive during mastering.
- Prevent conflicting actions while mastering is running.
- File picker and drag-and-drop must use the same application workflow.

## Async

- Do not block the UI thread during mastering, process execution, or file operations.
- Use asynchronous APIs where appropriate.
- Propagate `CancellationToken` through cancellable operations.
- Do not use `.Result` or `.Wait()`.

## Logging

- Application code depends on `ILogger<T>`.
- Serilog is the logging implementation.
- Keep technical diagnostics in logs and user-facing errors concise.

## C#

- Use modern idiomatic C# supported by .NET 10.
- Nullable reference types are enabled.
- Use file-scoped namespaces.
- Use the `I` prefix for interfaces.
- Use the `Async` suffix for asynchronous methods.
- Prefer strongly typed models.
- Follow existing project conventions.

## Validation

- Never suppress, disable, or ignore compiler, analyzer, nullable, or build warnings. Fix the underlying cause.
- Do not use `#pragma warning disable`, `NoWarn`, `SuppressMessage`, reduced analyzer severity, or similar mechanisms merely to make warnings disappear.
- After every code, XAML, project, dependency, or runtime configuration change, build the project.
- A task is not complete until the entire project builds with zero errors and zero warnings.
- After every such change, launch the application and verify that it starts successfully and does not crash during execution.
- If the change affects a specific user workflow, exercise that workflow and verify that it works without crashing.
- Never claim that a change is complete or working unless the required build and runtime verification have actually been performed.
- If the environment prevents building or running the application, explicitly report what could not be verified and why.