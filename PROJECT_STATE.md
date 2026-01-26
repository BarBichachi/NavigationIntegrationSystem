# NavigationIntegrationSystem – PROJECT_STATE

Get-ChildItem -Recurse -Force |
  Where-Object { $_.FullName -notmatch '\\(\.git|\.vs|bin|obj|PublishProfiles|Assets)(\\|$)' } |
  ForEach-Object { Resolve-Path -LiteralPath $_.FullName -Relative }

# Overview
NavigationIntegrationSystem (NIS) is a standalone WinUI 3 desktop application (net8.0-windows) designed to integrate, manage, and visualize INS devices.
The project is intended to be embedded later as a standalone project inside a larger solution (e.g. under a Utils folder), without runtime coupling to other projects.

The solution is structured as a clean, layered monorepo with strict separation of responsibilities.

# Architecture (Locked)

## Layered Solution Structure
src/
  NavigationIntegrationSystem.Core
  NavigationIntegrationSystem.Devices
  NavigationIntegrationSystem.Infrastructure
  NavigationIntegrationSystem.UI

Dependency direction is strictly one-way:
UI -> Devices -> Core
UI -> Infrastructure -> Core
Infrastructure -> Core
Devices -> Core

No reverse references are allowed.

---

## NavigationIntegrationSystem.Core
Purpose:
Pure domain and contracts layer.
Contains no UI code, no file I/O, no WinUI/Windows dependencies.

Responsibilities:
- Domain enums (e.g. DeviceType, DeviceStatus)
- Core device contracts (IInsDevice, device interfaces)
- Domain models (DeviceDefinition, DeviceFieldDefinition, etc.)
- Logging contracts (ILogService, LogRecord, LogLevel, ILogPaths)
- Any interfaces intended to be implemented by Infrastructure or Devices

Rules:
- No references to WinUI, Windows App SDK, file system, or concrete implementations
- Core is stable and changes rarely

---

## NavigationIntegrationSystem.Devices
Purpose:
All INS device-specific logic lives here.
Adding a new INS device should only require changes inside this project.

Responsibilities:
- INS device implementations (e.g. Vn310InsDevice, Tmaps100XInsDevice, ManualInsDevice)
- Device modules (IInsDeviceModule implementations)
- Device runtime registry and lifecycle management
- Device catalog / device metadata
- Device connection configuration models (UDP/TCP/Serial)
- Explicit device registration

Device Registration Model (Locked):
- Devices are registered explicitly (no reflection-based auto-discovery)
- There is a single registration entry point inside Devices
- UI/Bootstrap calls a single method to register all devices
- HostBuilderFactory is never modified when adding new devices

Adding a new device conceptually involves:
1. Add a new DeviceType value in Core
2. Add a new InsDevice implementation
3. Add a new DeviceModule implementation
4. Register the module in the Devices registration entry point

---

## NavigationIntegrationSystem.Infrastructure
Purpose:
Concrete implementations of Core contracts.

Responsibilities:
- File-based logging (FileLogService)
- Path resolution and application paths
- Application and logging settings
- Persistence of device configuration (files, JSON, etc.)

Rules:
- Implements Core interfaces only
- Owns file formats, paths, serialization
- UI never accesses files directly

---

## NavigationIntegrationSystem.UI
Purpose:
WinUI 3 presentation layer.

Responsibilities:
- Views (Pages, Panes)
- ViewModels (MVVM)
- UI-only services (navigation, dialogs, UI log buffer)
- Value converters
- Application bootstrap and DI wiring

MVVM Rules (Locked):
- No CommunityToolkit MVVM source generators
- No ObservableProperty / RelayCommand generators
- Manual MVVM implementation using a shared ViewModelBase
- ViewModelBase implements INotifyPropertyChanged with SetProperty
- Commands implemented via explicit ICommand implementations
- ViewModels contain UI logic only, no device-specific logic

UI Foldering Standard (Type-first):
- Enums/
- Interfaces/
- Services/
- Converters/
- ViewModels/
- Views/
- Navigation/
- Bootstrap/
- Resources/

Subfolders may be used inside type folders for domain clarity
(e.g. ViewModels/Integration/Candidates, ViewModels/Devices/Cards)

---

## Commands Architecture (Locked)
- Explicit ICommand implementations owned by UI
- No dependency on toolkit generators
- ICommand-based, explicit, debuggable, framework-agnostic
- Suitable for reuse in legacy .NET projects

---

## Target Frameworks
- NavigationIntegrationSystem.UI: net8.0-windows (WinUI 3)
- Other projects: compatible with net8.0 / net8.0-windows as required
- Solution is a container only; project configuration defines behavior

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
- Create a recorder service that writes a single append-only file containing:
  - Timestamp (UTC)
  - Per-row selected source (DeviceType / Manual / File)
  - Per-row input values
  - Per-row integrated output
- Start/Stop recording controls on Integration page
- Use JSONL format for safety and streaming

## 2) “Recorded file” as a virtual INS source
- Add a new virtual source option “File”
- Treat file playback exactly like a device source:
  - Appears in Active Sources
  - Appears per-row as a selectable candidate
- Provide:
  - Load file action
  - Basic playback controls
- Provide “Download template file” action with example rows

## 3) Make integration real (replace dummy tick)
- Replace Tick() random deltas with real device telemetry
- Devices publish field updates
- Candidate values update from live streams
- UI updates marshaled via DispatcherQueue
- Preserve existing selection, visibility, and fallback behavior
