# Copilot Instructions

## Project Context
- Solution targets `.NET 8`.
- Projects:
  - `src/NavigationIntegrationSystem.Core/NavigationIntegrationSystem.Core.csproj`
  - `src/NavigationIntegrationSystem.Infrastructure/NavigationIntegrationSystem.Infrastructure.csproj`
  - `src/NavigationIntegrationSystem.Devices/NavigationIntegrationSystem.Devices.csproj`
  - `src/NavigationIntegrationSystem.UI/NavigationIntegrationSystem.UI.csproj`

## General Guidelines
- Keep ViewModels decoupled from View logic.
- Interaction: no filler, direct code blocks, propose better architecture, prioritize maintainability, ask for missing files.
- Do not assume APIs or types exist unless they are present in the provided code.
- Use minimal changes to achieve the requested behavior.
- Prefer service-based architecture for I/O, persistence, and reusable business logic. ViewModels should orchestrate and delegate.
- Favor small, cohesive services with single responsibility and reusable helpers over repeated logic in ViewModels.
- Add a one-line comment above every new function or constructor.

## Code Style
- Use PascalCase for classes and properties.
- Use m_PascalCase for private fields.
- Use i_PascalCase for parameters.
- Use camelCase for local variables.
- Use [Action]Command for commands.
- Avoid using `var` unless in lambda expressions.
- Avoid temporary variable names.
- XAML: use explicit `Grid.RowDefinitions`/`Grid.ColumnDefinitions` and single-line elements.

## MVVM and ViewModels
- Use a manual `ViewModelBase` with `SetProperty`; do not use CommunityToolkit source generators.
- Commands must have explicit properties (e.g., `public IRelayCommand SaveCommand { get; }`).

## Class Structure
- Region order: Properties, Private Fields, Commands, Constructors, Functions, Event Handlers.
