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
Infrastructure -> Core
Devices -> Core
```

No reverse references are allowed.

---

# Key Files Reference

## Core Contracts
- IInsDevice: `src/NavigationIntegrationSystem.Core/Devices/IInsDevice.cs`
- ILogService: `src/NavigationIntegrationSystem.Core/Logging/ILogService.cs`

## Device Registration
- Device registration entry point: `src/NavigationIntegrationSystem.Devices/Runtime/DevicesModuleBootstrapper.cs`
- Device registry: `src/NavigationIntegrationSystem.Devices/Runtime/InsDeviceRegistry.cs`
- Device base class: `src/NavigationIntegrationSystem.Devices/Runtime/InsDeviceBase.cs`

## MVVM Base
- ViewModelBase: `src/NavigationIntegrationSystem.UI/ViewModels/Base/ViewModelBase.cs`

## DI Bootstrap
- HostBuilderFactory: `src/NavigationIntegrationSystem.UI/Bootstrap/HostBuilderFactory.cs`

## Infrastructure Services
- FileLogService: `src/NavigationIntegrationSystem.Infrastructure/Logging/FileLogService.cs`
- DevicesConfigService: `src/NavigationIntegrationSystem.Infrastructure/Persistence/DevicesConfig/DevicesConfigService.cs`

---

# Coding Conventions (Locked)

## General Principles
- Prefer clever, elegant solutions when they improve clarity and power
- Strongly prefer well-engineered, explicit architecture
- Architecture quality is a feature, not overhead
- Changes to existing structure are welcome when they improve design
- Refactoring may introduce new patterns or abstractions when justified
- Favor long-term maintainability over short-term convenience

## Naming

### C#
- Classes / Types: `PascalCase`
- Properties: `PascalCase`
- Private fields (non-properties): `m_PascalCase`
- Method parameters: `i_PascalCase`
- Local variables: `camelCase`
- Enums and enum values: `PascalCase`

### MVVM
- ViewModels: `[Feature]ViewModel`
- Commands: `[Action]Command` (e.g. `LoginCommand`, `ApplyToAllCommand`)
- Services: `[Purpose]Service`
- Pages / Views: `[Feature]Page`, `[Feature]View`

## Class Structure
- Every class must be organized using `#region` blocks
- Preferred region order:
  1. `Properties`
  2. `Private Fields`
  3. `Constructors`
  4. `Commands`
  5. `Functions`
  6. `Event Handlers`
- Functions should be short and focused
- Complex logic must be decomposed into sub-functions
- Prefer composable and testable units over monolithic logic

## Comments
- One-line `//` comment above every function
- Do not use XML `<summary>` comments
- Do not add comments on properties
- No trailing period at the end of comment lines
- Inline comments are allowed when logic is non-trivial or clever

## Properties
- Prefer one-line get/set property syntax:
  ```csharp
  public string DeviceId { get => m_DeviceId; set => SetProperty(ref m_DeviceId, value); }
  ```

## Commands (WinUI / MVVM)
- Commands must be declared explicitly as properties (no prefix, like regular properties)
- Commands belong under `#region Commands`
- Prefer explicit command interfaces (`IAsyncRelayCommand` / `IRelayCommand`)
- Do not use `[RelayCommand]` attributes
- Even simple or synchronous commands should be explicit

## Control Flow
- Braces are mandatory for all control blocks (`if`, `for`, `foreach`, `while`)
- Readability and predictability take precedence over brevity

## Async/Threading
- **Infrastructure/Devices**: Always use `ConfigureAwait(false)` (no UI context needed)
- **UI/ViewModels**: Never use `ConfigureAwait` (needs DispatcherQueue context)
- UI updates from non-UI code: always marshal via `DispatcherQueue.TryEnqueue()`

## Namespaces & Usings
- Always use `using` directives
- Avoid fully-qualified type names
- Keep namespace boundaries intentional and meaningful

## XAML Formatting
- Prefer single-line elements whenever possible
- Use multi-line formatting only when width or readability requires it
- Avoid unnecessary line breaks

## XAML Layout Structure (Critical for Hot Reload)
- Always prefer explicit `Grid` usage
- Always declare `Grid.RowDefinitions` and `Grid.ColumnDefinitions` explicitly
- Never define row or column definitions inline on the Grid tag
- This is required to ensure Hot Reload behaves correctly

## XAML Comments
- Comments inside XAML are allowed and encouraged
- Use XAML comments to explain layout intent or structural decisions
- Avoid comments that restate obvious XAML behavior

## Resources & Styling
- Converters must be registered globally in `App.xaml`
- Shared styles and resources are preferred over per-page definitions
- Maintain a consistent visual language across the application

## Refactoring Rules
- Show only added / removed / updated parts when refactoring
- Existing structure may be changed when there is a clear reason
- New architectural suggestions are welcome when they improve design quality
- Prefer improving abstractions over preserving legacy structure

---

# External Dependencies

## All Projects
- Microsoft.Extensions.DependencyInjection: v10.0.2
- Microsoft.Extensions.Hosting: v10.0.2
- Microsoft.Extensions.Logging: v10.0.2

## UI Project
- Microsoft.WindowsAppSDK: v1.8.260101001
- Microsoft.Windows.SDK.BuildTools: v10.0.26100.7463
- CommunityToolkit.Mvvm: v8.4.0 (interfaces only - no source generators)
- CommunityToolkit.WinUI.Collections: v8.2.251219
- System.Text.Json (implicit via .NET 8)

## Notes
- CommunityToolkit.Mvvm is referenced only for interfaces (`IAsyncRelayCommand`, `IRelayCommand`)
- No source generators (`[ObservableProperty]`, `[RelayCommand]`) are used
- All MVVM is implemented manually

---

# Project Layer Details

## NavigationIntegrationSystem.Core
**Purpose:**
Pure domain and contracts layer.
Contains no UI code, no file I/O, no WinUI/Windows dependencies.

**Responsibilities:**
- Domain enums (e.g. `DeviceType`, `DeviceStatus`)
- Core device contracts (`IInsDevice`, device interfaces)
- Domain models (`DeviceDefinition`, `DeviceFieldDefinition`, etc.)
- Logging contracts (`ILogService`, `LogRecord`, `LogLevel`, `ILogPaths`)
- Any interfaces intended to be implemented by Infrastructure or Devices

**Rules:**
- No references to WinUI, Windows App SDK, file system, or concrete implementations
- Core is stable and changes rarely

---

## NavigationIntegrationSystem.Devices
**Purpose:**
All INS device-specific logic lives here.
Adding a new INS device should only require changes inside this project.

**Responsibilities:**
- INS device implementations (e.g. `Vn310InsDevice`, `Tmaps100XInsDevice`, `ManualInsDevice`)
- Device modules (`IInsDeviceModule` implementations)
- Device runtime registry and lifecycle management
- Device catalog / device metadata
- Device connection configuration models (UDP/TCP/Serial)
- Explicit device registration

**Device Registration Model (Locked):**
- Devices are registered explicitly (no reflection-based auto-discovery)
- There is a single registration entry point inside Devices
- UI/Bootstrap calls a single method to register all devices
- HostBuilderFactory is never modified when adding new devices

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
- File-based logging (`FileLogService`)
- Path resolution and application paths
- Application and logging settings
- Persistence of device configuration (files, JSON, etc.)
- Binary data recording (matching existing RecordDecoderPro format)

**Rules:**
- Implements Core interfaces only
- Owns file formats, paths, serialization
- UI never accesses files directly

---

## NavigationIntegrationSystem.UI
**Purpose:**
WinUI 3 presentation layer.

**Responsibilities:**
- Views (Pages, Panes)
- ViewModels (MVVM)
- UI-only services (navigation, dialogs, UI log buffer)
- Value converters
- Application bootstrap and DI wiring

**MVVM Rules (Locked):**
- No CommunityToolkit MVVM source generators
- No `[ObservableProperty]` / `[RelayCommand]` generators
- Manual MVVM implementation using a shared `ViewModelBase`
- `ViewModelBase` implements `INotifyPropertyChanged` with `SetProperty`
- Commands implemented via explicit `ICommand` implementations
- ViewModels contain UI logic only, no device-specific logic

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
(e.g. `ViewModels/Integration/Candidates`, `ViewModels/Devices/Cards`)

---

## Commands Architecture (Locked)
- Explicit `ICommand` implementations owned by UI
- No dependency on toolkit generators
- ICommand-based, explicit, debuggable, framework-agnostic
- Suitable for reuse in legacy .NET projects

---

## Target Frameworks
- NavigationIntegrationSystem.UI: `net8.0-windows10.0.19041.0`
- Other projects: compatible with `net8.0` / `net8.0-windows` as required
- Solution is a container only; project configuration defines behavior

---

# Device Implementations (Current)

## Real Devices
1. **VN310** (`DeviceType.Vn310`)
   - Module: `Vn310DeviceModule`
   - Implementation: `Vn310InsDevice`
   - Connection: Not yet implemented

2. **TMAPS100X** (`DeviceType.Tmaps100X`)
   - Module: `Tmaps100XDeviceModule`
   - Implementation: `Tmaps100XInsDevice`
   - Connection: Not yet implemented

## Virtual Devices
3. **Manual** (`DeviceType.Manual`)
   - Module: `ManualDeviceModule`
   - Implementation: `ManualInsDevice`
   - No connection settings, no Inspect pane

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
- Parameter name column
- Integrated Output column
- One input candidate column per connected device
- Manual input option (RadioButton + TextBox)

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
- Integration recording will use a singleton instance of `BinaryFileRecorderEnhanced`
- Follows existing Infrastructure patterns
- Compatible with RecordDecoderPro tooling

---

# Key Decisions Log

## Why no CommunityToolkit.Mvvm source generators?
CommunityToolkit.Mvvm caused problems in past projects. Manual MVVM provides:
- Full control and visibility
- Easier debugging (no generated code)
- Better for team members unfamiliar with source generators
- More suitable for potential legacy .NET integration

## Why explicit device registration?
Explicit registration makes it easy for other developers to:
- Discover what devices exist (single registration point)
- Add new devices without "magic" reflection
- Understand the full device list at compile-time
- Catch registration errors early

## Why binary .dat files for recording?
- Must match existing RecordDecoderPro tool format
- Integration with larger system requires format consistency
- Binary is compact and efficient for high-frequency recording
- Streaming-friendly for playback

## Why singleton for BinaryFileRecorderEnhanced?
- Matches existing larger system architecture
- Single recording session across all subsystems
- Centralized file management and disk space monitoring

---

# Current State (Implemented)

## Integration Page (UI locked + working)
- Integration grid exists and is functional:
  - Rows: Azimuth, Elevation, Latitude, Longitude, Altitude, Pitch, Roll, Speed (dummy for now)
  - Columns: Parameter, Integrated Output, per-device inputs
- Connected devices appear in the header as "Active Sources":
  - Each device has:
    - Visibility toggle (show/hide its inputs across all rows)
    - "Apply to All" (select this device as the source for all rows)
- Manual source supported:
  - Manual appears as a virtual device
  - Manual input uses a RadioButton + TextBox per row
  - TextBox enabled only when selected
- Live dummy updates:
  - Candidate values update via DispatcherQueueTimer (250ms)
  - Integrated output reflects the currently selected candidate per row

## Devices / Connection interaction
- Integration listens to device Status changes:
  - Rebuilds ConnectedDevices header list
  - Rebuilds per-row Sources
  - Refreshes VisibleSources based on header toggles
- Selection survives device rebuilds by re-selecting by DeviceType when possible
- Manual device:
  - Has Connect/Disconnect only
  - No Inspect or Settings panes

## Logs Page (baseline improvements)
- Keyboard shortcuts supported: Ctrl+C, Ctrl+A, Delete, Esc
- Tooltip disabled on list items
- Filters/actions row + layout fixes applied
- Main window hamburger pane sizing fix applied

---

# Next Steps

## 1) Record inputs + outputs to a single file (MVP)
- Create a recorder service in Infrastructure that:
  - Uses singleton `BinaryFileRecorderEnhanced` pattern
  - Writes binary `.dat` format matching RecordDecoderPro
- Start/Stop recording controls on Integration page
- Compatible with existing RecordDecoderPro tool

## 2) "Recorded file" as a virtual INS source
- Add a new virtual source option "File"
- Treat file playback exactly like a device source:
  - Appears in Active Sources
  - Appears per-row as a selectable candidate
- Provide:
  - Load file action
  - Basic playback controls (play, pause, seek)
  - Playback speed control
- Provide "Download template file" action with example rows

## 3) Make integration real (replace dummy tick)
- Replace Tick() random deltas with real device telemetry
- Devices publish field updates
- Candidate values update from live streams
- UI updates marshaled via DispatcherQueue
- Preserve existing selection, visibility, and fallback behavior


---

# Integrated INS Output Recording (DataRecordType = 50) — LOCKED

## Purpose
Add a unified “Integrated INS Output” binary record that represents the **final integrated output per field**, while preserving full source traceability.
The record must be fully compatible with the existing RecordDecoderPro tool **without modifying its core logic**.

This is achieved by introducing a new `DataRecordType`, a dedicated binary payload + comm frame, and a RecordDecoderPro Item template.

---

## Record Type (Locked)
- `Infrastructure.Enums.DataRecordType.IntegratedInsOutputRawData = 50`
- RecordDecoderPro handles it via a new Item:
- case DataRecordType.IntegratedInsOutputRawData: IntegratedInsOutputItem.InitializeDict(header, rawData); break;

- 
---

## Integrated Fields (Final Order — Locked)

The following order is final and must not change:

1. **Output Time**
 - OutputTime[hms]
 - OutputTime[sec]

2. **Position**
 - Latitude
 - Longitude
 - Altitude

3. **Euler Angles**
 - Roll
 - Pitch
 - Azimuth
 - Roll Rate
 - Pitch Rate
 - Azimuth Rate

4. **Velocity**
 - Velocity (total)
 - Velocity North
 - Velocity East
 - Velocity Down

5. **Status**
 - StatusValue (bitmask)

6. **Course**

---

## Time Convention (Locked)

### Record Receive Time
- Comes from `header.Time`
- Stored as `DateTime.ToBinary()` (`long`)
- Decoded exactly like other items:
- `DateTime.FromBinary(header.Time)`
- CSV columns:
  - `RcvTime[hms]`
  - `RcvTime[sec]`

### Output Time
- Stored inside the payload as `DateTime.ToBinary()` (`long`)
- Decoded the same way as other device times (e.g. `TmapsTime`)
- CSV columns:
- `OutputTime[hms]`
- `OutputTime[sec]`

---

## Per-Field Source Triplet (Locked)

Each integrated field is represented by **three columns**:

- `[FieldName]Device` -> `ushort` (DeviceCode enum value)
- `[FieldName]Id`     -> `ushort` (device instance index per DeviceCode)
- `[FieldName]Value`  -> `double`

This applies to **all numeric fields**.

Example: LatitudeDevice, LatitudeId, LatitudeValue


---

## Binary Types (Locked)

| Field            | Type     |
|------------------|----------|
| DeviceCode       | ushort   |
| DeviceId         | ushort   |
| Value            | double   |
| OutputTimeBinary | long     |
| StatusValue      | uint     |

---

## Status Model (Locked)

- All vendor-specific INS statuses are normalized into a **single `StatusValue` bitmask**
- Defined in `IntegratedInsOutputStatusFlags`
- Mapping from VN310 / Tmaps / StandardIns happens **outside** RecordDecoderPro
- Bit allocation is documented in code comments and treated as locked once committed

---

## Header.ID (Locked)
- `header.ID` for integrated records is always `0`
- Per-field source identity is represented by `(DeviceCode, DeviceId)`
- No reliance on `header.ID` for source tracking

---

## Implemented Files (Current State)

The following files were created and currently live under `Temp/` (to be moved later):

- `IntegratedInsOutput_Data.cs`
- `IntegratedInsOutput_CommFrame.cs`
- `IntegratedInsOutputStatusFlags.cs`
- `IntegratedInsOutputItem.cs`

### Responsibilities
- **_Data**: Pure payload model (similar to `VN310_InsData`, `Tmaps100_InsData`)
- **_CommFrame**: Binary encode/decode wrapper (similar to `StdInsCommFrame`)
- **StatusFlags**: Unified status bitmask definition
- **Item**: RecordDecoderPro CSV mapping + column naming

---

## What’s Left To Do (For End-to-End Functionality)

### 1. Finalize Binary Layout
- Lock exact write/read order inside `IntegratedInsOutput_CommFrame`
- Ensure `EncodeBinaryData` and `DecodeBinaryData` are perfectly symmetric

### 2. Finalize RecordDecoderPro Item
- Ensure column list matches locked field order
- Decode:
  - `RcvTime` from `header.Time`
  - `OutputTime` from payload
- Emit 3 columns per field consistently

### 3. Move Files Out of Temp
- Infrastructure files -> proper `Infrastructure.Navigation...` namespaces
- Item file -> `RecordDecoderPro.ItemTemplates`

### 4. Recording Side (NIS / Infrastructure)
- Build `IntegratedInsOutput_Data` from selected per-row sources
- Assign `DeviceCode` + `DeviceId` per field
- Populate `OutputTime` based on selected source
- Populate `StatusValue`
- Write record using:
  - `DataType = 50`
  - `Time = DateTime.UtcNow.ToBinary()`

### 5. Device Instance Indexing
- Maintain per-DeviceType instance counters
- Stable IDs per session
- Used consistently across integrated fields

### 6. Mapping Logic (Later Phase)
- Per-field source selection
- “Apply to All” behavior
- Manual device numeric input
- Vendor-specific status -> unified status bitmask mapping

---

## Stop Point
At this point:
- Schema is fully locked
- Binary contract shape is defined
- Decoder strategy is aligned with existing tooling
- Files exist but are not yet wired end-to-end

Next continuation starts with:
**validating the binary layout and wiring encode -> record -> decode -> CSV**