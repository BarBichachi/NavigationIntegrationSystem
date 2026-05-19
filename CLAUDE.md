# NavigationIntegrationSystem — CLAUDE.md

## Project Purpose

NIS is a standalone WinUI 3 desktop application that integrates multiple INS devices, lets the operator select the authoritative source per navigation parameter (azimuth, elevation, lat/lon/alt, pitch, roll, speed), and records the integrated output to binary `.dat` files compatible with RecordDecoderPro. Designed to be embedded into a larger solution later with no runtime coupling.

---

## Architecture

```
src/
  NavigationIntegrationSystem.Core           ← domain contracts only, no I/O
  NavigationIntegrationSystem.Devices        ← all INS device logic
  NavigationIntegrationSystem.Infrastructure ← concrete service implementations
  NavigationIntegrationSystem.UI             ← WinUI 3 presentation layer
```

Dependency direction is strictly one-way:
```
UI → Devices → Core
UI → Infrastructure → Core
```

Core has no references to anything else. Infrastructure and Devices never reference UI.

---

## Tech Stack

| | |
|---|---|
| Platform | .NET 8, WinUI 3, Windows App SDK 1.8 |
| UI target | `net8.0-windows10.0.19041.0` |
| MVVM | Manual — `CommunityToolkit.Mvvm` for interfaces only (`IRelayCommand`, `IAsyncRelayCommand`) |
| DI / Hosting | `Microsoft.Extensions.Hosting` + `Microsoft.Extensions.DependencyInjection` |
| Logging | `Microsoft.Extensions.Logging` + `FileLogService` |
| Collections | `CommunityToolkit.WinUI.Collections` |
| Serialization | `System.Text.Json` |

---

## Build

```
dotnet build NavigationIntegrationSystem.slnx
```

Only run this for: new project references, DI registration changes, or complex type restructuring. Skip it for routine property/method edits — the user is running VS and will catch compile errors immediately.

---

## Naming Conventions

| | |
|---|---|
| Classes / Types / Properties | `PascalCase` |
| Private fields | `m_PascalCase` (e.g. `m_IsConnected`) |
| Method parameters | `i_PascalCase` (e.g. `i_Device`) |
| Local variables | `camelCase` |
| Commands | `[Action]Command` (e.g. `ApplySettingsCommand`) |
| ViewModels | `[Feature]ViewModel` |
| Services | `[Purpose]Service` |
| Pages / Views | `[Feature]Page`, `[Feature]View` |

---

## Class Structure — Strict Region Order

Every class must use `#region` blocks in this order:

1. `#region Properties`
2. `#region Private Fields`
3. `#region Commands`
4. `#region Constructors`
5. `#region Functions`
6. `#region Event Handlers`

---

## MVVM Rules (Locked)

- `ViewModelBase` implements `INotifyPropertyChanged` with `SetProperty`
- One-line property syntax: `public string X { get => m_X; set => SetProperty(ref m_X, value); }`
- Commands are explicit properties under `#region Commands`, typed as `IRelayCommand` or `IAsyncRelayCommand`
- **Forbidden:** `[ObservableProperty]`, `[RelayCommand]`, or any CommunityToolkit source generators
- **`partial` is required on every VM** — CsWinRT (the WinUI 3 marshalling source generator) emits partial extensions for any type that crosses the WinRT ABI (e.g. INotifyPropertyChanged set as DataContext, used via x:Bind). Missing `partial` works on JIT today but breaks under trimming / AOT and triggers the WinRT info diagnostic on the affected type.
- ViewModels contain UI logic only — no device-specific logic, no file I/O

---

## Code Style

- No `var` (except in lambda expressions)
- No temporary or abbreviated variable names
- Braces mandatory on all control blocks
- One-line `//` comment above every function — no XML `<summary>` tags, no comments on properties
- No trailing period on comment lines
- Prefer one-liners for simple guards and properties
- Functions must be short and focused; decompose complex logic into sub-functions

---

## Async / Threading

- **Infrastructure / Devices:** always `ConfigureAwait(false)` — no UI context needed
- **UI / ViewModels:** never `ConfigureAwait` — needs DispatcherQueue context
- UI updates from non-UI code: always marshal via `DispatcherQueue.TryEnqueue()`

---

## XAML Rules

- Prefer single-line elements
- **Hot Reload rule:** always declare `Grid.RowDefinitions` and `Grid.ColumnDefinitions` explicitly — never inline on the Grid tag
- Converters registered globally in `App.xaml`
- Shared styles preferred over per-page definitions
- XAML comments are encouraged to explain layout intent

---

## Device Registration Pattern (Locked)

- Explicit registration only — no reflection-based auto-discovery
- Single entry point: `DevicesModuleBootstrapper` (registered before any service requests)
- UI/Bootstrap calls one method; `HostBuilderFactory` is never modified when adding devices
- Adding a device: `DeviceType` enum in Core → `InsDevice` impl in Devices → `DeviceModule` in Devices → register in bootstrapper

---

## Key Files

| | |
|---|---|
| Core device contract | `src/NavigationIntegrationSystem.Core/Devices/IInsDevice.cs` |
| Log contract | `src/NavigationIntegrationSystem.Core/Logging/ILogService.cs` |
| Device bootstrapper | `src/NavigationIntegrationSystem.Devices/Runtime/DevicesModuleBootstrapper.cs` |
| Device registry | `src/NavigationIntegrationSystem.Devices/Runtime/InsDeviceRegistry.cs` |
| Device base class | `src/NavigationIntegrationSystem.Devices/Runtime/InsDeviceBase.cs` |
| DI bootstrap | `src/NavigationIntegrationSystem.UI/Bootstrap/HostBuilderFactory.cs` |
| ViewModel base | `src/NavigationIntegrationSystem.UI/ViewModels/Base/ViewModelBase.cs` |
| File log service | `src/NavigationIntegrationSystem.Infrastructure/Logging/FileLogService.cs` |
| Devices config service | `src/NavigationIntegrationSystem.Infrastructure/Persistence/DevicesConfig/DevicesConfigService.cs` |
| Integration snapshot | `src/NavigationIntegrationSystem.UI/Services/Recording/IntegrationSnapshotService.cs` |

---

## Current Devices

| Device | Type | Status |
|---|---|---|
| VN310 | `DeviceType.Vn310` | Impl done, connection not yet wired |
| TMAPS100X | `DeviceType.Tmaps100X` | Impl done, connection not yet wired |
| Manual | `DeviceType.Manual` | Done — no connection settings |
| Playback | `DeviceType.Playback` | **In progress** — next major feature |

---

## Integration Parameters

Grid rows (in order): Azimuth, Elevation, Latitude, Longitude, Altitude, Pitch, Roll, Speed.
Each row: parameter name | integrated output | one column per connected device | manual input (RadioButton + TextBox).

---

## Binary Recording Format

File: `.dat`, compatible with RecordDecoderPro. Record layout:
```
[SyncWord: 2 bytes] [ID: 2 bytes] [DataLength: 2 bytes]
[Time: 8 bytes (DateTime.ToBinary())] [DataType: 2 bytes] [Data: DataLength bytes]
```
`DataRecordType.IntegratedInsOutputRawData = 50`

---

## What's Left (Next Feature: Playback Device)

- CSV playback engine: streaming `StreamReader`, timing engine (delta between timestamps for 1x speed), state machine (Play/Pause/Stop/Seek), data dispatcher
- `PlaybackInsDevice` implementation inheriting `InsDeviceBase`
- Playback settings pane: file picker, export template button, loop toggle
- Global playback tray at bottom of `MainWindow`: play/pause, stop, seeker slider — visible only when Playback device is Connected
- Column injection into `IntegrationViewModel` when Playback device connects
- Then: real device telemetry (replace `Tick()` dummy data with actual parsing)

---

## Files Staged for Deletion

`src/NavigationIntegrationSystem.Infrastructure/TO_BE_DELETED/` — legacy files from the main solution, kept temporarily as reference. Do not modify these.
