# NavigationIntegrationSystem – PROJECT_STATE

Get-ChildItem -Recurse -Force |
>>   Where-Object { $_.FullName -notmatch '\\(\.git|\.vs|bin|obj|PublishProfiles|Assets)(\\|$)' } |
>>   ForEach-Object { Resolve-Path -LiteralPath $_.FullName -Relative }

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

## Tooling Requirements
- Visual Studio 2022 (17.8+ recommended)
- .NET 8 SDK
- Windows App SDK
- Windows 10/11 SDK (10.0.22621 or newer)

---

# Next Steps (Planned Refactor)

## Phase 1 – MVVM Foundation
- Introduce ViewModelBase in UI
- Introduce RelayCommand / AsyncRelayCommand
- Remove all toolkit property generators
- Refactor existing ViewModels to manual MVVM

## Phase 2 – UI Folder Restructure
- Apply type-first foldering standard
- Move Enums, Services, Converters, Navigation into consistent locations
- Split ViewModels into Pages and Panes where applicable

## Phase 3 – Devices Registration Cleanup
- Introduce single Devices registration entry point
- Remove device registration logic from HostBuilderFactory
- Ensure adding a device never touches UI or Infrastructure

## Phase 4 – Logging Simplification
- Review logging pipeline end-to-end
- Ensure clear separation between Core contracts, Infrastructure sinks, and UI buffers
- Reduce unnecessary indirection if present

## Phase 5 – Configuration & Persistence Review
- Verify clean separation between config models and storage logic
- Introduce interfaces if required to decouple persistence from consumers

---

# Notes
- Architecture decisions are locked unless explicitly revised
- Refactors prioritize clarity, maintainability, and explicit wiring over magic
- Adding new INS devices must remain low-risk and localized
