Here is the updated **PROJECT_STATE.md**. I have overhauled the "Current State" and "Next Steps" sections to reflect that the recording logic, data mapping, and validation tools are now fully implemented and verified.

```markdown
// ---------------------------------------------------------
// GENERATED: 2026-02-08 12:20:00
// PROJECT: NavigationIntegrationSystem (WinUI 3 / C#)
// ---------------------------------------------------------


// ---------------------------------------------------------
// FILE: .\PROJECT_STATE.md
// ---------------------------------------------------------

# NavigationIntegrationSystem – PROJECT_STATE

# File Listing Powershell Command
```powershell
Get-ChildItem -Recurse -Force |
  Where-Object { $_.FullName -notmatch '\\(\.git|\.vs|bin|obj|PublishProfiles|Assets)(\\|$)' } |
  ForEach-Object { Resolve-Path -LiteralPath $_.FullName -Relative }

```

# Overview

NavigationIntegrationSystem (NIS) is a standalone WinUI 3 desktop application (net8.0-windows) designed to integrate, manage, and visualize INS devices.
The project is intended to be embedded later as a standalone project inside a larger solution (e.g. under a Utils folder), without runtime coupling to other projects.

The solution is structured as a clean, layered monorepo with strict separation of responsibilities.

# Architecture (Locked)

## Layered Solution Structure

```
src/
  NavigationIntegrationSystem.Core
  NavigationIntegrationSystem.Devices
  NavigationIntegrationSystem.Infrastructure
  NavigationIntegrationSystem.UI

```

Dependency direction is strictly one-way:

```
UI -> Devices -> Core
UI -> Infrastructure -> Core

```

---

# Key Files Reference

## Core Contracts

* IInsDevice: `src/NavigationIntegrationSystem.Core/Devices/IInsDevice.cs`
* ILogService: `src/NavigationIntegrationSystem.Core/Logging/ILogService.cs`

## Device Registration

* Device registration entry point: `src/NavigationIntegrationSystem.Devices/Runtime/DevicesModuleBootstrapper.cs`
* Device registry: `src/NavigationIntegrationSystem.Devices/Runtime/InsDeviceRegistry.cs`
* Device base class: `src/NavigationIntegrationSystem.Devices/Runtime/InsDeviceBase.cs`

## MVVM Base

* ViewModelBase: `src/NavigationIntegrationSystem.UI/ViewModels/Base/ViewModelBase.cs`

## DI Bootstrap

* HostBuilderFactory: `src/NavigationIntegrationSystem.UI/Bootstrap/HostBuilderFactory.cs`

## Infrastructure Services

* FileLogService: `src/NavigationIntegrationSystem.Infrastructure/Logging/FileLogService.cs`
* DevicesConfigService: `src/NavigationIntegrationSystem.Infrastructure/Persistence/DevicesConfig/DevicesConfigService.cs`

---

# Coding Conventions (Locked)

## General Principles

* Prefer clever, elegant solutions when they improve clarity and power
* Strongly prefer well-engineered, explicit architecture
* Architecture quality is a feature, not overhead
* Changes to existing structure are welcome when they improve design
* Refactoring may introduce new patterns or abstractions when justified
* Favor long-term maintainability over short-term convenience

## Naming

### C#

* Classes / Types: `PascalCase`
* Properties: `PascalCase`
* Private fields (non-properties): `m_PascalCase`
* Method parameters: `i_PascalCase`
* Local variables: `camelCase`
* Enums and enum values: `PascalCase`

### MVVM

* ViewModels: `[Feature]ViewModel`
* Commands: `[Action]Command` (e.g. `LoginCommand`, `ApplyToAllCommand`)
* Services: `[Purpose]Service`
* Pages / Views: `[Feature]Page`, `[Feature]View`

## Class Structure

* Every class must be organized using `#region` blocks
* Preferred region order:
1. `Properties`
2. `Private Fields`
3. `Commands`
4. `Constructors`
5. `Functions`
6. `Event Handlers`


* Functions should be short and focused
* Complex logic must be decomposed into sub-functions
* Prefer composable and testable units over monolithic logic

## Comments

* One-line `//` comment above every function
* Do not use XML `<summary>` comments
* Do not add comments on properties
* No trailing period at the end of comment lines
* Inline comments are allowed when logic is non-trivial or clever

## Properties

* Prefer one-line get/set property syntax:
```csharp
public string DeviceId { get => m_DeviceId; set => SetProperty(ref m_DeviceId, value); }

```



## Commands (WinUI / MVVM)

* Commands must be declared explicitly as properties (no prefix, like regular properties)
* Commands belong under `#region Commands`
* Prefer explicit command interfaces (`IAsyncRelayCommand` / `IRelayCommand`)
* Do not use `[RelayCommand]` attributes
* Even simple or synchronous commands should be explicit

## Control Flow

* Braces are mandatory for all control blocks (`if`, `for`, `foreach`, `while`)
* Readability and predictability take precedence over brevity

## Async/Threading

* **Infrastructure/Devices**: Always use `ConfigureAwait(false)` (no UI context needed)
* **UI/ViewModels**: Never use `ConfigureAwait` (needs DispatcherQueue context)
* UI updates from non-UI code: always marshal via `DispatcherQueue.TryEnqueue()`

## Namespaces & Usings

* Always use `using` directives
* Avoid fully-qualified type names
* Keep namespace boundaries intentional and meaningful

## XAML Formatting

* Prefer single-line elements whenever possible
* Use multi-line formatting only when width or readability requires it
* Avoid unnecessary line breaks

## XAML Layout Structure (Critical for Hot Reload)

* Always prefer explicit `Grid` usage
* Always declare `Grid.RowDefinitions` and `Grid.ColumnDefinitions` explicitly
* Never define row or column definitions inline on the Grid tag
* This is required to ensure Hot Reload behaves correctly

## XAML Comments

* Comments inside XAML are allowed and encouraged
* Use XAML comments to explain layout intent or structural decisions
* Avoid comments that restate obvious XAML behavior

## Resources & Styling

* Converters must be registered globally in `App.xaml`
* Shared styles and resources are preferred over per-page definitions
* Maintain a consistent visual language across the application

## Refactoring Rules

* Show only added / removed / updated parts when refactoring
* Existing structure may be changed when there is a clear reason
* New architectural suggestions are welcome when they improve design quality
* Prefer improving abstractions over preserving legacy structure

---

# External Dependencies

## All Projects

* Microsoft.Extensions.DependencyInjection: v10.0.2
* Microsoft.Extensions.Hosting: v10.0.2
* Microsoft.Extensions.Logging: v10.0.2

## UI Project

* Microsoft.WindowsAppSDK: v1.8.260101001
* Microsoft.Windows.SDK.BuildTools: v10.0.26100.7463
* CommunityToolkit.Mvvm: v8.4.0 (interfaces only - no source generators)
* CommunityToolkit.WinUI.Collections: v8.2.251219
* System.Text.Json (implicit via .NET 8)

## Notes

* CommunityToolkit.Mvvm is referenced only for interfaces (`IAsyncRelayCommand`, `IRelayCommand`)
* No source generators (`[ObservableProperty]`, `[RelayCommand]`) are used
* All MVVM is implemented manually

---

# Project Layer Details

## NavigationIntegrationSystem.Core

**Purpose:**
Pure domain and contracts layer.
Contains no UI code, no file I/O, no WinUI/Windows dependencies.

**Responsibilities:**

* Domain enums (e.g. `DeviceType`, `DeviceStatus`)
* Core device contracts (`IInsDevice`, device interfaces)
* Domain models (`DeviceDefinition`, `DeviceFieldDefinition`, etc.)
* Logging contracts (`ILogService`, `LogRecord`, `LogLevel`, `ILogPaths`)
* Any interfaces intended to be implemented by Infrastructure or Devices

**Rules:**

* No references to WinUI, Windows App SDK, file system, or concrete implementations
* Core is stable and changes rarely

---

## NavigationIntegrationSystem.Devices

**Purpose:**
All INS device-specific logic lives here.
Adding a new INS device should only require changes inside this project.

**Responsibilities:**

* INS device implementations (e.g. `Vn310InsDevice`, `Tmaps100XInsDevice`, `ManualInsDevice`)
* Device modules (`IInsDeviceModule` implementations)
* Device runtime registry and lifecycle management
* Device catalog / device metadata
* Device connection configuration models (UDP/TCP/Serial)
* Explicit device registration

**Device Registration Model (Locked):**

* Devices are registered explicitly (no reflection-based auto-discovery)
* There is a single registration entry point inside Devices
* UI/Bootstrap calls a single method to register all devices
* HostBuilderFactory is never modified when adding new devices

**Adding a new device conceptually involves:**

1. Add a new `DeviceType` value in Core
2. Add a new `InsDevice` implementation in Devices
3. Add a new `DeviceModule` implementation in Devices
4. Register the module in the Devices registration entry point

---

## NavigationIntegrationSystem.Infrastructure

**Purpose:**
Concrete implementations of Core contracts.

**Responsibilities:**

* File-based logging (`FileLogService`)
* Path resolution and application paths
* Application and logging settings
* Persistence of device configuration (files, JSON, etc.)
* Binary data recording (matching existing RecordDecoderPro format)

**Rules:**

* Implements Core interfaces only
* Owns file formats, paths, serialization
* UI never accesses files directly

---

## NavigationIntegrationSystem.UI

**Purpose:**
WinUI 3 presentation layer.

**Responsibilities:**

* Views (Pages, Panes)
* ViewModels (MVVM)
* UI-only services (navigation, dialogs, UI log buffer)
* Value converters
* Application bootstrap and DI wiring

**MVVM Rules (Locked):**

* No CommunityToolkit MVVM source generators
* No `[ObservableProperty]` / `[RelayCommand]` generators
* Manual MVVM implementation using a shared `ViewModelBase`
* `ViewModelBase` implements `INotifyPropertyChanged` with `SetProperty`
* Commands implemented via explicit `ICommand` implementations
* ViewModels contain UI logic only, no device-specific logic

**UI Foldering Standard (Type-first):**

```
Enums/
Interfaces/
Services/
Converters/
ViewModels/
Views/
Navigation/
Bootstrap/
Resources/

```

Subfolders may be used inside type folders for domain clarity

---

## Commands Architecture (Locked)

* Explicit `ICommand` implementations owned by UI
* No dependency on toolkit generators
* ICommand-based, explicit, debuggable, framework-agnostic
* Suitable for reuse in legacy .NET projects

---

## Target Frameworks

* NavigationIntegrationSystem.UI: `net8.0-windows10.0.19041.0`
* Other projects: compatible with `net8.0` / `net8.0-windows` as required
* Solution is a container only; project configuration defines behavior

---

# Device Implementations (Current)

## Real Devices

1. **VN310** (`DeviceType.Vn310`)
* Module: `Vn310DeviceModule`
* Implementation: `Vn310InsDevice`
* Connection: Not yet implemented


2. **TMAPS100X** (`DeviceType.Tmaps100X`)
* Module: `Tmaps100XDeviceModule`
* Implementation: `Tmaps100XInsDevice`
* Connection: Not yet implemented



## Virtual Devices

3. **Manual** (`DeviceType.Manual`)
* Module: `ManualDeviceModule`
* Implementation: `ManualInsDevice`
* No connection settings, no Inspect pane



---

# Integration Parameters (Current)

**Grid Rows (in order):**

1. Azimuth
2. Elevation
3. Latitude
4. Longitude
5. Altitude
6. Pitch
7. Roll
8. Speed

**Each row has:**

* Parameter name column
* Integrated Output column
* One input candidate column per connected device
* Manual input option (RadioButton + TextBox)

**Note:** These parameters are current and may evolve.

---

# Binary Recording Format (Locked)

## Overview

NIS uses binary `.dat` file recording to match the existing RecordDecoderPro tool format from the larger system.

## Standard Binary Record Structure

Based on the existing `BinaryFileRecorderEnhanced` pattern:

```
[SyncWord: 2 bytes (ushort)]
[ID: 2 bytes (ushort)]
[DataLength: 2 bytes (ushort)]
[Time: 8 bytes (long - DateTime.ToBinary())]
[DataType: 2 bytes (ushort)]
[Raw data bytes: DataLength bytes]

```

## Recording Service

* Integration recording will use a singleton instance of `BinaryFileRecorderEnhanced`
* Follows existing Infrastructure patterns
* Compatible with RecordDecoderPro tooling

---

# Current State (Implemented)

## 1. Integration Logic & Recording (DONE)

* **Snapshot Service:** `IntegrationSnapshotService` captures UI state at 50Hz/100Hz.
* **Data Mapping:** Mapped `IntegrationFieldRowViewModel` to `IntegratedInsOutput_Data`.
* **Clone Handling:** Implemented logic to correctly update nested immutable structures (`Position`, `EulerData`, etc.).
* **Calculated Fields:** Logic added to calculate `Velocity Total` magnitude from N/E/D components before recording.
* **Status Bitmask:** Logic implemented to set `StatusValue` bits for every populated field.
* **Recording Service:** `NisRecordingService` wraps the legacy `BinaryFileRecorderEnhanced`.
* **Zero-Fill Prevention:** Implemented flushing/dummy write to ensure files are not 0KB.

## 2. Validation Tools (DONE - Temporary)

* **CSV Testing:** `CsvTestingService` mirrors every binary snapshot to a human-readable CSV.
* **Settings Page:** Added "Open Recordings Folder" button.
* **Manual Input:** `ManualSourceCandidateViewModel` correctly parses user text to doubles for testing.

## 3. Architecture & Startup (DONE)

* **App Startup Fix:** `App.xaml.cs` modified to register `DevicesModuleBootstrapper` **before** any service requests to prevent `NotSupportedException`.
* **Integration Guide:** `INTEGRATION_GUIDE.md` created in `TO_BE_DELETED` folder to map the transition to the main solution.

## 4. UI (DONE)

* **Integration Page:** Full grid with selectable sources.
* **Shell:** Recording controls in TitleBar.
* **Devices Page:** Cards, settings pane, inspect pane.
* **Logs Page:** Live logs.

---

# Integrated INS Output Recording (DataRecordType = 50) — LOCKED

## Purpose

Add a unified “Integrated INS Output” binary record that represents the **final integrated output per field**,
while preserving full source traceability. The record must be fully compatible with the existing RecordDecoderPro
tool **without modifying its core logic**.

---

## Record Type (Locked)

* `DataRecordType.IntegratedInsOutputRawData = 50`

## Binary Types (Locked)

| Field | Type |
| --- | --- |
| DeviceCode | ushort |
| DeviceId | ushort |
| Value | double |
| OutputTimeBinary | long |
| StatusValue | uint |

---

## Status Model (Locked)

* All vendor-specific INS statuses are normalized into a **single `StatusValue` bitmask**
* Defined in `IntegratedInsOutputStatusFlags`
* Mapping happens inside `IntegrationSnapshotService`

---

## Implemented Files (To Be Moved)

These files currently live in Infrastructure/UI but are destined for the main solution:

* `IntegratedInsOutput_Data.cs` (Shared Infra)
* `IntegratedInsOutput_CommFrame.cs` (Shared Infra)
* `IntegratedInsOutputStatusFlags.cs` (Shared Infra)
* `IntegratedInsOutputItem.cs` (RecordDecoderPro)

---

# Next Steps

## 1. Feature Implementation

### 1.1 Core Infrastructure (Playback Service)
- [ ] **CSV Data Engine:** Implement line-by-line `StreamReader` to handle large files without memory spikes.
- [ ] **Timing Engine:** Logic to calculate deltas between CSV timestamps to maintain authentic 1x playback speed.
- [ ] **State Machine:** Track Play/Pause/Stop/Seek states and the current line index/total lines.
- [ ] **Data Dispatcher:** Broadcast the current CSV row as a telemetry object to the Playback Device.

### 1.2 Device Domain (Playback Device)
- [ ] **Type Registration:** Add `DeviceType.Playback` to Core Enums.
- [ ] **Device Implementation:** Create `PlaybackInsDevice` (inherits `InsDeviceBase`).
- [ ] **Lifecycle Management:** - Disconnected: No file selected.
    - Ready: File validated (header check).
    - Connected: Playback column active in Integration Grid.
- [ ] **Telemetry Mapping:** Map CSV columns to `InspectFields` for live viewing in the Inspect pane.

### 1.3 UI & UX (Settings & Controls)
- [ ] **Playback Settings Pane:** - Implement File Picker for `.csv` selection.
    - Implement **"Export Playback Template"** button (generates CSV with required headers).
    - Add "Loop" toggle.
- [ ] **Global Playback Tray:**
    - Create a persistent bottom bar in `MainWindow`.
    - Controls: Play/Pause, Stop (Reset), and a Seeker Slider (Line 0 to Line Count).
    - Visibility: Only visible when the Playback Device is "Connected".

### 1.4 Integration Logic
- [ ] **Column Injection:** Ensure `IntegrationViewModel` detects the Playback device and adds the column automatically.
- [ ] **Source Selection:** Allow users to select "Playback" as the source for any specific field (Lat, Lon, Roll, etc.).


* Real Device Telemetry: Replace Tick() dummy random deltas with actual data parsing from connected devices.

## 2. Final Verification

* Run full recording session with mixed sources (Manual + Real).

## 3. Integration (The Move)

* Follow `INTEGRATION_GUIDE.md`.
* Delete `TO_BE_DELETED` folder contents.
* Replace shims with real production references.
* Move `IntegratedInsOutputItem.cs` to decoder project.
* Verify `.dat` file integrity in RecordDecoderPro.
