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
- INS device implementations (e.g. Vn310InsDevice, Tmaps100XInsDevice)
- Device modules (IInsDeviceModule implementations)
- Device runtime registry and lifecycle management
- Device catalog / device metadata
- Device connection configuration models (UDP/TCP/Serial)
- Explicit device registration

Device Registration Model (Locked):
- Devices are registered explicitly (no reflection-based auto-discovery)
- There is a single registration entry point inside Devices (e.g. DevicesRegistration)
- UI/Bootstrap calls a single method to register all devices
- HostBuilderFactory is never modified when adding new devices

Adding a new device conceptually involves:
1. Add a new DeviceType value in Core
2. Add a new InsDevice implementation
3. Add a new DeviceModule implementation
4. Register the module in the Devices registration file

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
- Commands implemented via an internal RelayCommand / AsyncRelayCommand abstraction
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
(e.g. Services/Logging, Converters/Devices, Enums/Devices)

---

## Commands Architecture (Locked)
- Custom RelayCommand / AsyncRelayCommand implementation owned by UI
- No dependency on toolkit generators
- ICommand-based, explicit, debuggable, framework-agnostic
- Suitable for reuse in .NET Framework 4.7.2 projects

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
- Live dummy updates:
  - Candidate values update via DispatcherQueueTimer (250ms)
  - Integrated output reflects the currently selected candidate per row

## Devices / Connection interaction
- Integration listens to device Status changes:
  - Rebuilds ConnectedDevices header list
  - Rebuilds per-row Sources
  - Refreshes VisibleSources based on header toggles
- Selection survives device rebuilds by re-selecting by DeviceType when possible

## Logs Page (baseline improvements)
- Keyboard shortcuts supported: Ctrl+C, Ctrl+A, Delete, Esc
- Tooltip disabled on list items
- Filters/actions row + layout fixes applied
- Main window hamburger pane sizing fix applied

---

# Next Steps

## 1) Manual source per field (UI + VM)
- Add a per-row "Manual" source option alongside device candidates
- When "Manual" is selected:
  - Show an inline input (TextBox) for numeric value
  - Validate (double.TryParse), show invalid state but don’t crash selection
  - Manual value becomes the row’s SelectedValueText / integrated output
- Ensure manual selection survives visibility toggles and device reconnects (same selection model as today: IsSelected + row-enforced single selection)

## 2) Record inputs + outputs to a single file (MVP)
- Create a recorder service (UI/Infrastructure boundary decision later) that writes a single file containing:
  - Timestamp (UTC)
  - Per-row selected source (DeviceType or Manual or File)
  - Per-row input values (all visible candidates, plus selected)
  - Per-row integrated output
- Start/Stop recording controls on Integration page
- Use append-only format (JSONL recommended) so it’s safe for long runs and easy to stream

## 3) “Recorded file” as a virtual INS source
- Add a new virtual source option “File” (per device-like candidate) that feeds values from a loaded recording file
- Provide:
  - “Load file” action (file picker) and playback controls (Play/Pause/Seek/Speed optional, can be v1 minimal)
  - Treat the file source exactly like an INS device in the UI (appears in Active Sources + per-row candidates)
- Add “Download template file” action:
  - Writes a template (CSV/JSONL) that shows required columns/fields and an example row
  - Clear mapping: FieldName + Unit + Value (+ optional DeviceId/DeviceType if you want multiple streams)

## 4) Make integration real (replace dummy tick)
- Replace Tick() random deltas with real device telemetry:
  - Each device publishes field updates (Azimuth, Elevation, etc.)
  - CandidateValue updates from that stream
  - UI updates are marshaled via DispatcherQueue
- Keep the same selection/visibility behavior:
  - Selected source drives integrated output immediately
  - Fallback rules when a source disappears (manual/file/device disconnect)
