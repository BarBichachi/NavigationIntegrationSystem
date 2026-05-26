# VN310 Real-Telemetry Implementation Plan

> **For Claude resuming this work after `/clear`:** read this file end-to-end
> before touching code. It captures every decision already made so you don't
> need to re-derive them from chat history.

## Goal (one sentence)

Wire the VN310 INS device to provide real telemetry into NIS's integration
grid, replacing the random-walk dummy values, mirroring the pattern that
Playback already established (`PlaybackInsDevice` ↔ `PlaybackSourceCandidateViewModel`).

## Glossary - what code lives where

So you don't confuse external research with shipping code:

- **NIS** - this repo (`C:\Users\BARBIC\source\repos\NavigationIntegrationSystem`).
  Everything we ship lives here.
- **Parent solution** - the eventual larger umbrella that NIS will be folded
  into. Identity / conventions unknown until merge time. Only known fact: the
  files under `Infrastructure/TO_BE_DELETED/` came from it and represent its
  real schema for things like the integrated INS output binary record.
- **External research references** - read-only codebases on the user's Desktop,
  studied to learn the VN310 protocol. They are NOT the parent solution and
  their conventions are NOT authoritative for NIS:
  - `C:\Users\BARBIC\Desktop\Work\NavigationControlSystem\Navigation-LEAN\VectorNav\VN310\V5.0\` - VectorNav's vendor demo + SDK source.
  - `C:\Users\BARBIC\Desktop\Work\NavigationControlSystem\Navigation-LEAN\NavigationSystemsController\` - an internal app (`OrbitNavSystemCtrl` family) that consumes VN310. Useful as a working integration example.
  - `C:\Users\BARBIC\Desktop\Work\NavigationControlSystem\Navigation\Navigation\NavigationSystemsController\` - same family, slightly different copy. The two diff-empty for VN310 code. The second copy contains the `packages/VectorNav.1.1.5/` NuGet directory.
  Treat all three as "vendor docs in code form" - useful for cross-checking
  protocol details, *not* a template for how NIS organizes its code.

---

## Locked Design Decisions (do not re-litigate)

Every decision below was settled in conversation; treat them as facts.

### Transport & protocol
- **Physical link:** Serial (RS-232 / USB-Serial). Default `COM1` / `115200` baud.
  Editable in connection settings.
- **Protocol:** Whatever the VN310's factory configuration emits. We do NOT
  reconfigure the unit on connect. The user trusts factory config; their team
  agrees nobody changes it.
- **Mode handling:** Parse whatever arrives. `VectorNavLib.PacketFinder` surfaces
  both ASCII and binary packets. Handler branches on `packet.Type`:
  - Binary → check `IsCompatible(expectedGroups)` → extract → otherwise skip + log
  - ASCII VNINS → `ParseVNINS()`
  - Anything else (NMEA `$GP…`, other VNINS variants) → silently ignore
- **Dual-stream supported (Phase 8):** The sensor can be factory-configured to
  emit ASCII VNINS *and* a binary subscription concurrently (e.g. ASCII 4 Hz +
  Binary 100 Hz). The service decodes each packet independently into a per-source
  latest snapshot (`m_LatestAscii`, `m_LatestBinary`) and composes a merged
  `Vn310Telemetry` on every packet: shared fields from the freshest source;
  Binary-exclusive fields (rates + TimeStatus) from Binary if fresh; ASCII-exclusive
  fields (uncertainties) from ASCII if fresh. Per-source staleness window: 2s
  (matches the device-level watchdog). Both single-stream configurations
  (legacy ASCII-only or Binary-only units) degrade naturally - the missing
  source's freshness flag is permanently false and its exclusive fields stay
  zeroed.
- **Expected binary groups** (when factory configured for binary):
  - `CommonGroup` = `YawPitchRoll | AngularRate | Position | Velocity | InsStatus`
  - `TimeGroup` = `TimeUtc | TimeStatus`
- **Per-source field availability the UI surfaces:**
  - ASCII VNINS carries uncertainties (`AttU`, `PosU`, `VelU`) but NOT angular
    rates or TimeStatus. UTC needs the `-18s` leap-second hack (accepted;
    documented).
  - Binary CommonGroup+TimeGroup carries angular rates + TimeStatus + raw Y/M/D
    UTC, but NOT uncertainties (would require subscribing `AttitudeGroup` /
    `InsGroup`).
  - In dual-stream mode every field is live. In single-stream mode, the
    missing-source exclusive fields render as `-` and the inspect view shows
    a banner explaining which side is silent.

### Units & conventions
- VN's "Yaw" output → maps to NIS's integration row labelled `Yaw` (renamed
  from `Azimuth` in this conversation).
- Yaw wire range: VN sends -180…+180°. **NIS wraps to 0…360°** for display
  consistency. One-liner: `(yaw + 360) % 360`.
- Lat/Lon: degrees on the wire, store + display as degrees.
- Alt: meters.
- NED velocity: m/s.
- Speed: derived = `sqrt(N² + E² + D²)`. Not on the wire.
- Angular rates (binary only): degrees/sec on the wire.

### Library
- **VectorNav SDK:** add NuGet `VectorNav` v1.1.5 (same package the parent
  solution uses). It ships `net472` only; .NET 8 loads it via netstandard2.0
  compatibility.
- **Fallback** if NuGet doesn't resolve cleanly on .NET 8: drop
  `VectorNav.dll` into `lib/VectorNav/` and add a `<Reference HintPath=...>`.
  This needs to be decided in Phase 1 (the bring-up phase) when we know if it
  works.
- **`System.IO.Ports`:** add NuGet package (required separately on .NET 8).

### Layout (no per-INS projects)
NIS keeps a single `.Devices` project. VN310-specific code lives under:
```
src/NavigationIntegrationSystem.Devices/
  Implementations/
    Vn310/                                  ← new subfolder
      Vn310InsDevice.cs                     ← moved here from Implementations/
      Vn310TelemetryService.cs              ← new
      Vn310Telemetry.cs                     ← new (payload type)
      Vn310InsStatus.cs                     ← new (lifted from VN310_Statusses.cs)
      Vn310TimeStatus.cs                    ← new (lifted from VN310_Statusses.cs)
      Vn310InsMode.cs                       ← new (enum)
  Modules/
    Vn310DeviceModule.cs                    ← stays put; rewritten field set
```

UI-side:
```
src/NavigationIntegrationSystem.UI/
  ViewModels/Integration/Candidates/
    Vn310SourceCandidateViewModel.cs        ← new, mirrors Playback candidate
  ViewModels/Inspect/
    Vn310InspectViewModel.cs                ← new
  Views/Inspect/
    Vn310InspectPage.xaml(.cs)              ← new
  Views/Panes/SubViews/
    Vn310SettingsView.xaml(.cs)             ← new (connection settings pane)
  Views/Controls/
    RecommendedHint.xaml(.cs)               ← new reusable (used by all INS panes)
```

Core / config:
```
src/NavigationIntegrationSystem.Core/
  Integration/
    IntegrationFieldKeyMap.cs               ← extend with Vn310 sub-map
  Models/DeviceCatalog/
    RecommendedConnectionSettings.cs        ← new (optional metadata on DeviceDefinition)
```

### UI behavior
- **Per-INS inspect page.** Generic `InspectPageBase` (header, status panel slot,
  raw-data panel slot); `Vn310InspectPage` fills the slots. TMAPS / others get
  their own later.
- **Per-INS connection settings pane.** Generic for now (reuse existing
  `SerialConnectionSettings`), VN310-specific only if needed.
- **Recommended-hint mechanism:** small reusable `RecommendedHint` control with
  one `Text` property; collapses when text is null/empty. Placed above every
  input where a hint may apply. Hint data lives on a `Recommended` block
  attached to the `DeviceDefinition`.
- **InsMode badge** on the VN310 device card: color-coded chip showing
  `TRACKING` (green) / `ALIGNING` (yellow) / `GNSS LOSS` (orange) /
  `NOT TRACKING` (red). Updates from latest packet.
- **Watchdog:** 2 seconds of no packets → device status transitions to `Error`
  with message `"No telemetry for 2s"`.

### Out of scope (do NOT do in this implementation)
- Sensor reconfiguration on connect (binary config writes). Forbidden.
- A "VN310 simulator" replay tool. Nice-to-have but real hardware is coming.
- NMEA `$GP…` parsing - VN310 emits these on the same port; we ignore them
  silently. NIS itself has no need for them; if the eventual parent solution
  wants them, that's handled outside NIS.
- Multi-instance VN310 support (more than one VN310 connected at once).
  Currently NIS is single-instance-per-device-type. Don't change that here.
- Per-device-card raw-data tap / packet logger. Future feature.
- ASCII VNINS leap-second table (just hardcode `-18` with a comment).

---

## Reference points in existing code (study these before writing code)

When you sit down to implement, **read these files first** - they're the
templates for the new code:

| New file you're writing | Mirror this existing file |
|---|---|
| `Vn310TelemetryService.cs` | `Infrastructure/Playback/CsvPlaybackService.cs` (lifecycle, event raising) AND `MainService.cs` from `Navigation-LEAN/VectorNav/VN310/V5.0/MainService.cs` (VnSensor connect/subscribe shape) |
| `Vn310InsDevice.cs` (rewrite) | `Devices/Implementations/PlaybackInsDevice.cs` (Telemetry dict, OnConnect/OnDisconnect lifecycle) |
| `Vn310SourceCandidateViewModel.cs` | `UI/ViewModels/Integration/Candidates/PlaybackSourceCandidateViewModel.cs` (Volatile.Read/Write pattern, Tick + GetSnapshotValue, IDisposable) |
| `Vn310InsStatus.cs` / `Vn310TimeStatus.cs` | `Navigation-LEAN/VectorNav/VN310/V5.0/VN310_Statusses.cs` (lift the bit-decoding; adapt naming to NIS conventions) |
| `Vn310DeviceModule.cs` (rewrite) | Current shape stays; field list comes from the production reference at `NavigationSystemsController/Infrastructures/Infrastructure.Navigation/NavigationSystems/VectorNav_VN310/VN310_InsData.cs` |
| `RecommendedHint.xaml` | (no exact mirror; greenfield) |

External reference for VectorNav SDK usage (read-only, do not vendor source):
- `C:\Users\BARBIC\Desktop\Work\NavigationControlSystem\Navigation\Navigation\NavigationSystemsController\Utils\NavigationSystemsController\Services\VectorNavService.cs`
- Note both the working ASCII branch AND the commented-out binary branch.

---

## Phases

Each phase is a complete, mergeable unit. After each phase: code compiles, app
still launches, nothing previously working is broken. Phases can be done in
order without skipping.

---

### Phase 1 - Library bring-up

**Goal:** Reference VectorNav SDK successfully from `.Devices`. Confirm a
`VnSensor` instance can be constructed at runtime on .NET 8 without crashing.

**Steps:**
1. Add to `NavigationIntegrationSystem.Devices.csproj`:
   ```xml
   <PackageReference Include="VectorNav" Version="1.1.5" />
   <PackageReference Include="System.IO.Ports" Version="..." />  <!-- latest stable -->
   ```
2. `dotnet restore` - confirm package resolves on .NET 8.
3. If it doesn't (net472 binary won't load): drop `VectorNav.dll` into
   `lib/VectorNav/`. A copy exists on disk under
   `C:\Users\BARBIC\Desktop\Work\NavigationControlSystem\Navigation\Navigation\NavigationSystemsController\packages\VectorNav.1.1.5\lib\net472\VectorNav.dll`
   (external research artifact - NOT part of NIS or its parent solution; it
   was copied there by another project unrelated to ours).
   Replace the PackageReference with a `<Reference Include="VectorNav">` +
   `<HintPath>`. Document which path was taken in `MIGRATION_NOTES.md`.
4. Add a temporary smoke-test invocation somewhere innocuous (e.g. construct
   `new VnSensor()` in an existing service's constructor inside a `try/catch`,
   log success/failure). Run the app once.
5. Remove the smoke-test code before committing.

**Definition of done:**
- `dotnet build` succeeds.
- App launches without exceptions.
- Either NuGet package reference or vendored DLL is committed, with the choice
  noted in MIGRATION_NOTES section 3.

**Don't do in this phase:** any business logic, any new files under
`Implementations/Vn310/`. This is library plumbing only.

---

### Phase 2 - Telemetry service + parsing (no UI yet)

**Goal:** A standalone `Vn310TelemetryService` that opens a serial port,
subscribes to VectorNav packets, parses what it expects, and raises a
`TelemetryUpdated` event with a strongly-typed payload. No integration with
`Vn310InsDevice` yet - verify in isolation first.

**New files:**

`src/NavigationIntegrationSystem.Devices/Implementations/Vn310/Vn310InsMode.cs`
- Enum: `NotTracking = 0, Aligning, Tracking, GnssLoss` (matches VN's wire encoding).

`src/NavigationIntegrationSystem.Devices/Implementations/Vn310/Vn310InsStatus.cs`
- Class wrapping `ushort Rawdata`.
- Bit-decoded properties (lift from VN310_Statusses.cs, adapt to NIS conventions):
  - `Mode` (returns `Vn310InsMode`)
  - `IsGpsFix`, `IsGpsHeadingIns`, `IsGpsCompassActive`
  - `MeasurementError` (4 bits) with nested decoder for `IsImuError`,
    `IsMagnetometerError`, `IsGnssError`
- `Clone()` method.

`src/NavigationIntegrationSystem.Devices/Implementations/Vn310/Vn310TimeStatus.cs`
- Class wrapping `byte Rawdata`.
- Bit-decoded: `IsTimeOK`, `IsDateOK`, `IsUtcTimeValid`, `IsValid` (composite).
- `Clone()` method.

`src/NavigationIntegrationSystem.Devices/Implementations/Vn310/Vn310Telemetry.cs`
- Immutable payload type representing the merged snapshot across both sources
  (ASCII VNINS + Binary CommonGroup+TimeGroup):
  - `DateTime UtcTime` (Binary if fresh; else ASCII with leap-hack date)
  - `double LatDeg, LonDeg, AltM` (shared - freshest source wins)
  - `double YawDeg` (already wrapped 0…360), `PitchDeg`, `RollDeg` (shared)
  - `double YawRateDegS, PitchRateDegS, RollRateDegS` (Binary-exclusive; zero
    when `IsBinaryFresh == false`)
  - `double VelNorth, VelEast, VelDown, Speed` (shared)
  - `float AttUncertainty, PosUncertainty, VelUncertainty` (ASCII-exclusive;
    zero when `IsAsciiFresh == false`)
  - `Vn310InsStatus InsStatus` (shared)
  - `Vn310TimeStatus TimeStatus` (Binary-exclusive; default when not fresh)
  - `bool IsAsciiFresh` - ASCII packet received within the per-source staleness
    window (2s) at the moment this snapshot was published. Drives whether
    uncertainty fields carry real values.
  - `bool IsBinaryFresh` - same, for the Binary stream. Drives whether rates
    and `TimeStatus` carry real values.
  - `DateTime PacketReceivedAt` (system clock at the merge-publishing instant)

`src/NavigationIntegrationSystem.Devices/Implementations/Vn310/Vn310TelemetryService.cs`
- Per-device-instance service. NOT a singleton.
- Properties:
  - `bool IsConnected { get; }`
  - `Vn310Telemetry? LatestTelemetry { get; }`
  - `string? LastError { get; }`
- Events:
  - `event EventHandler<Vn310Telemetry>? TelemetryUpdated`
  - `event EventHandler? Stalled` (raised on watchdog 2s timeout)
- Methods:
  - `Task StartAsync(string i_ComPort, int i_BaudRate, CancellationToken i_CancellationToken)`
  - `Task StopAsync()`
- Internals:
  - Owns a `VnSensor`. Wires `AsyncPacketReceived` to handler.
  - Handler logic (`OnAsyncPacket`) - SDK packet thread is the single writer
    of per-source state, so per-source reads/writes need no lock; only the
    timestamp rings and the watchdog timer ref are lock-guarded (they're read
    on UI / disposed on another thread):
    - Bail on `packet.IsError` (log + ignore).
    - `packet.Type == PacketType.Ascii`:
      - If `IsAsciiAsync == false` → ignore (NMEA passthrough).
      - If `AsciiAsyncType != VNINS` → ignore (other VN ASCII async types).
      - Else `ParseVNINS(...)` → `m_LatestAscii = parsed`. UTC computed as
        `i_ReceivedAt.Date + TimeSpan.FromSeconds((time - 18) % 86400)` (NOTE:
        `-18` is the GPS↔UTC leap-second offset; will silently drift if a leap
        second is ever added - accepted limitation).
    - `packet.Type == PacketType.Binary`:
      - `IsCompatible(expectedCommonGroup, expectedTimeGroup, None, None, None, None)`
        - if false, log "incompatible binary packet" once per minute
        (rate-limited) and skip. Don't spam logs.
      - Else extract in this exact order (locked by VN310 ICD serialization):
        1. `vec3f` YPR
        2. `vec3f` angular rates
        3. `vec3d` LLA
        4. `vec3f` NED velocity
        5. `ushort` InsStatus
        6. Y/M/D/H/M/S/ms bytes + `byte` TimeStatus
      - `m_LatestBinary = parsed`. UTC comes directly from the bytes (no
        `-18` hack).
    - After routing, call `BuildMerged(now)` to compose the published
      `Vn310Telemetry`:
      - `asciiFresh = (now - m_LatestAscii.PacketReceivedAt) < 2s`; same
        for `binaryFresh`.
      - Shared fields (YPR, LLA, NED, InsStatus) → from whichever per-source
        snapshot has the higher `PacketReceivedAt` (tie / one-source-stale
        falls back accordingly).
      - Binary-exclusive fields (rates, TimeStatus) → from `m_LatestBinary`
        if `binaryFresh`, else zero / default.
      - ASCII-exclusive fields (uncertainties) → from `m_LatestAscii` if
        `asciiFresh`, else `0f`.
      - `UtcTime` → prefer Binary if fresh (real Y/M/D validated by
        TimeStatus); fall back to ASCII otherwise.
      - `IsAsciiFresh` / `IsBinaryFresh` set on the merged result.
    - Publish: `Volatile.Write(ref m_LatestTelemetry, merged)`.
    - Log source-mode transitions (Unknown→Both, Both→AsciiOnly when Binary
      stalls, recovery, etc.) lifecycle-only - no per-packet noise.
  - Per-source stats:
    - `AsciiPacketCount`, `BinaryPacketCount` (Interlocked counters).
    - `GetRecentAsciiTimestamps()`, `GetRecentBinaryTimestamps()` - 1s
      ring (cap 256 each) for Hz computation, guarded by `m_StatsLock`.
    - `LastSourceMode` (`AsciiOnly` / `BinaryOnly` / `Both` / `Unknown`)
      derived live from the published snapshot's freshness flags - single
      source of truth.
  - Wraps yaw to 0…360 inside ParseAscii / ParseBinary.
  - Computes `Speed = sqrt(N² + E² + D²)` per source.
  - Watchdog: `System.Threading.Timer` with 2s period, reset on ANY packet
    of either source. On expiry → raise `Stalled` event, set `LastError`.
    Per-source staleness (also 2s) is independent: a single-source stall
    doesn't trigger the device-level watchdog (the other source keeps
    flowing), it just flips that source's `Fresh` flag to false and zeros
    its exclusive fields on the next merge.

**Definition of done:**
- New files compile.
- A unit-test-style smoke test (or temporary console invocation) confirms the
  service can be `StartAsync`'d against a non-existent port and produce a
  reasonable error message (no crash).
- No integration with `Vn310InsDevice` yet - that's Phase 3.

**Don't do in this phase:**
- Touch `Vn310InsDevice.cs` or `Vn310DeviceModule.cs`.
- Any UI work.
- Any integration grid candidate work.

---

### Phase 3 - Wire `Vn310InsDevice` to the telemetry service

**Goal:** When the user clicks Connect on the VN310 device card, real packets
start flowing. Device exposes `LatestTelemetry` and an event for subscribers.

**Steps:**
1. Move `Vn310InsDevice.cs` from `Implementations/` to `Implementations/Vn310/`.
2. Rewrite `Vn310InsDevice`:
   - Composes a `Vn310TelemetryService` (created in constructor, owned for the
     device's lifetime).
   - `OnConnectAsync()`:
     - Read `Config.Connection.Serial.ComPort` and `.BaudRate`.
     - Call `service.StartAsync(comPort, baudRate, cancellationToken)`.
     - Subscribe to `service.TelemetryUpdated` and `service.Stalled`.
   - `OnDisconnectAsync()`:
     - Unsubscribe.
     - Call `service.StopAsync()`.
   - Exposes:
     - `Vn310Telemetry? LatestTelemetry => m_Service.LatestTelemetry;`
     - `event EventHandler<Vn310Telemetry>? TelemetryUpdated` (forwarded
       from the inner service).
   - Stall handler: call `SetStatus(Error, "No telemetry for 2s")`.
3. Rewrite `Vn310DeviceModule.BuildDefinition()` with the real field set:
   ```
   UtcTime          / "UTC Time"          / ""
   LatDeg           / "Latitude"          / "deg"
   LonDeg           / "Longitude"         / "deg"
   AltM             / "Altitude"          / "m"
   YawDeg           / "Yaw"               / "deg"
   PitchDeg         / "Pitch"             / "deg"
   RollDeg          / "Roll"              / "deg"
   YawRateDegS      / "Yaw Rate"          / "deg/s"
   PitchRateDegS    / "Pitch Rate"        / "deg/s"
   RollRateDegS    / "Roll Rate"          / "deg/s"
   VelNorth         / "Vel North"         / "m/s"
   VelEast          / "Vel East"          / "m/s"
   VelDown          / "Vel Down"          / "m/s"
   Speed            / "Speed"             / "m/s"
   AttUncertainty   / "Att Uncertainty"   / ""
   PosUncertainty   / "Pos Uncertainty"   / ""
   VelUncertainty   / "Vel Uncertainty"   / ""
   InsStatus        / "INS Status"        / ""
   TimeStatus       / "Time Status"       / ""
   ```
   Note Yaw/Pitch/Roll order matches the integration grid.

**Definition of done:**
- Clicking Connect on VN310 with a real (or simulated) port opens it,
  packets arrive in `Vn310InsDevice.LatestTelemetry`. Manual verification
  via debugger or temporary log line.
- Disconnect cleanly closes the port.
- Bad port name produces a friendly error and `DeviceStatus.Error`.

---

### Phase 4 - Integration grid candidate (`Vn310SourceCandidateViewModel`)

**Goal:** When VN310 is connected, its values appear as a selectable source
in the integration grid rows.

**Steps:**
1. Create `Vn310SourceCandidateViewModel.cs` in
   `UI/ViewModels/Integration/Candidates/`. Pattern: clone of
   `PlaybackSourceCandidateViewModel`, but:
   - Constructor takes `Vn310InsDevice i_Device` (cast from `IInsDevice`) and
     `string i_TelemetryFieldKey` (e.g., `"YawDeg"`).
   - Subscribes to `i_Device.TelemetryUpdated`.
   - On packet arrival: extract the relevant field via the key, `Volatile.Write`.
   - `Tick()` and `GetSnapshotValue()` follow the same pattern as Playback.
   - `Dispose()` unsubscribes.

2. Extend `IntegrationFieldKeyMap.cs` to support per-device-type lookup:
   - Either add a new `Vn310` dictionary alongside `FieldToCsvKey`, or
   - Refactor to a single `IReadOnlyDictionary<(DeviceType, string), string>`.
   - Choose the lower-churn option (probably the former: keep
     `FieldToCsvKey` as-is for Playback; add `FieldToVn310Key`).
   - VN310 key mapping:
     ```
     Latitude     → LatDeg
     Longitude    → LonDeg
     Altitude     → AltM
     Yaw          → YawDeg
     Pitch        → PitchDeg
     Roll         → RollDeg
     YawRate      → YawRateDegS
     PitchRate    → PitchRateDegS
     RollRate     → RollRateDegS
     VelocityNorth → VelNorth
     VelocityEast  → VelEast
     VelocityDown  → VelDown
     Course       → (not provided by VN310, omit)
     ```

3. Extend `IntegrationViewModel.RebuildRowSources` - add a branch for
   `DeviceType.VN310` alongside the existing Playback branch:
   ```csharp
   if (device.Type == DeviceType.VN310)
   {
       if (IntegrationFieldKeyMap.FieldToVn310Key.TryGetValue(row.FieldName, out string? vnKey))
       {
           row.Sources.Add(new Vn310SourceCandidateViewModel((Vn310InsDevice)device.Device, device.DisplayName, vnKey));
       }
       continue;
   }
   ```

**Definition of done:**
- Connect VN310 → VN310 column appears in the integration grid.
- Selecting VN310 as the source for Latitude shows live latitude updates.
- Selecting VN310 for Yaw Rate shows 0 when factory is in ASCII mode,
  real values when binary.
- Disconnect → VN310 column disappears, no resource leak (Dispose called on
  candidates).

---

### Phase 5 - Connection settings UI + RecommendedHint mechanism

**Goal:** The VN310 device's connection settings pane lets the user pick
COM port and baud rate, with recommended-default hints displayed above the
relevant inputs.

**Steps:**

1. Create `UI/Views/Controls/RecommendedHint.xaml(.cs)`:
   - A `UserControl` with one dependency property `Text` (string).
   - Renders italic gray text (style defined in `App.xaml`).
   - When `Text` is null/empty → `Visibility = Collapsed`.

2. Add `Core/Models/DeviceCatalog/RecommendedConnectionSettings.cs`:
   - Class with optional string properties for hints:
     - `KindHint`, `ComPortHint`, `BaudRateHint`, `RemoteIpHint`,
       `RemotePortHint`, `LocalIpHint`, `LocalPortHint`.
   - All nullable; only set the ones that apply per INS.

3. Extend `DeviceDefinition` with an optional
   `RecommendedConnectionSettings? RecommendedConnection { get; }` property.

4. Set VN310's recommendations in `Vn310DeviceModule.BuildDefinition()`:
   ```csharp
   RecommendedConnection = new RecommendedConnectionSettings
   {
       KindHint = "Recommended: Serial",
       BaudRateHint = "Recommended: 115200",
   }
   ```

5. Create `UI/Views/Panes/SubViews/Vn310SettingsView.xaml(.cs)`:
   - Mirrors the structure of `RealDeviceSettingsView` (the generic one
     currently used for VN310/TMAPS).
   - Each input has a `<controls:RecommendedHint />` above it, bound to the
     matching hint property.

6. Wire the device-settings pane router so that VN310 uses
   `Vn310SettingsView` instead of `RealDeviceSettingsView`.

**Definition of done:**
- Selecting VN310 in the devices list shows the new settings pane.
- Hints render above Kind and Baud Rate inputs.
- No hints render for other inputs (COM Port has no recommendation;
  the hint control collapses cleanly).
- Saving + reconnecting honors the new settings.

**Note:** This phase also unblocks adding hints for other INS's later
(TMAPS, etc.) by simply populating their `RecommendedConnection`.

---

### Phase 6 - Status surfaces (device card badge + inspect view)

**Naming note:** the original plan said "inspect page". The actual NIS
architecture renders inspect as a **side pane** on `DevicesPage`, not a
navigated full page. Implementation uses `Vn310InspectView` (mirroring
`Vn310SettingsView` from Phase 5.5), not `Vn310InspectPage`.

**Goal:** User can see at a glance whether VN310 is `TRACKING` /
`ALIGNING` / `GNSS LOSS` / `NOT TRACKING`, and can drill into a per-INS
inspect view showing all decoded telemetry + status flags.

**Steps as implemented:**

1. **Mode badge plumbing** (Core + UI):
   - `Core/Devices/DeviceModeSeverity.cs` - enum
     `Unknown / Good / Warning / Caution / Bad`.
   - `Core/Devices/DeviceModeSnapshot.cs` - immutable `{Label, Severity}`.
   - `IInsDevice` gains `DeviceModeSnapshot? CurrentMode` + `event ModeChanged`.
     Default impl on `InsDeviceBase` returns null + never raises;
     `Vn310InsDevice` overrides + raises from
     `OnServiceTelemetryUpdated` and `OnServiceStalled` (also clears on
     `OnDisconnectAsync`).
   - `UI/Converters/DeviceModeSeverityToBrushConverter.cs` (Good=green
     `16,124,16`, Warning=amber `202,138,4`, Caution=orange `217,119,6`,
     Bad=red `196,43,28`, Unknown=gray) + `DeviceModeSeverityToVisibilityConverter.cs`
     (collapse on Unknown).
   - `UI/Views/Controls/DeviceModeChip.xaml(.cs)` - reusable pill with
     two DPs (`Label` string, `Severity` int-backed for WinRT-friendliness).
   - `DeviceCardViewModel` exposes flattened `ModeLabel` / `ModeSeverity`
     and subscribes to `ModeChanged` via the existing DispatcherQueue
     marshalling pattern.
   - `DevicesPage.xaml` device-card data template renders the chip next
     to the connection Status text.
2. **Inspect pane refactor** mirroring Phase 5.5 (settings) exactly:
   - `DeviceInspectPaneViewModelBase` (abstract partial, IDisposable).
   - `GenericInspectPaneViewModel` - placeholder for TMAPS (wraps
     existing `InspectFields`).
   - `Vn310InspectPaneViewModel` - bespoke (see step 4).
   - `DeviceInspectPaneFactory.Create(DeviceCardViewModel)` switch.
   - `InspectPaneTemplateSelector` (mirrors `SettingsPaneTemplateSelector`).
   - `Views/Panes/SubViews/GenericInspectView.xaml` - moved out of the
     host; `Views/Panes/SubViews/Vn310InspectView.xaml` - bespoke.
   - `DevicesViewModel` adds `CurrentInspectPane` property + factory
     dispatch + `DisposeCurrentInspectPane()` (called on open settings,
     open inspect, close, cleanup).
3. **Packet stats** on `Vn310TelemetryService` (dual-stream aware):
   - `long PacketCount` (Interlocked sum of both sources),
     `long AsciiPacketCount`, `long BinaryPacketCount` (per-source counters).
   - `Vn310PacketSourceMode LastSourceMode` - `AsciiOnly` / `BinaryOnly`
     / `Both` / `Unknown`, derived live from the published snapshot's
     freshness flags.
   - `DateTime[] GetRecentAsciiTimestamps()`,
     `DateTime[] GetRecentBinaryTimestamps()` - per-source 1s ring (cap
     256 each). Updated in `OnAsyncPacketReceived` under `m_StatsLock`.
   - All forwarded through `Vn310InsDevice` so the inspect VM doesn't
     reach into `m_Service`.
4. **`Vn310InspectPaneViewModel`** - subscribes to `TelemetryUpdated`,
   marshals to UI thread, plus a 250ms `DispatcherTimer` to advance
   "last packet age" and per-source Hz between packets. Sections:
   - **Dual-stream banner** (success/green) - visible when both
     `IsAsciiFresh` and `IsBinaryFresh` are true. "Dual stream active -
     ASCII (uncertainties) and Binary (rates + TimeStatus) both
     flowing. All fields live."
   - **ASCII-only banner** (info/blue) - visible when only `IsAsciiFresh`.
     "ASCII stream only - rates and TimeStatus unavailable. Enable
     Binary Output 1 (CommonGroup + TimeGroup) in factory config to
     enable rate fields."
   - **Binary-only banner** (info/blue) - visible when only `IsBinaryFresh`.
     "Binary stream only - uncertainty fields unavailable. Enable ASCII
     VNINS in factory config to populate AttU / PosU / VelU." (Asymmetry
     is real: ASCII gives uncertainties but no rates; Binary gives rates
     but no uncertainties - dual stream resolves it.)
   - **Current telemetry** - UTC, Lat/Lon/Alt, Y/P/R, rates, NED vel,
     speed, three uncertainties. Cells render `-` when the corresponding
     source isn't fresh (rates → `IsBinaryFresh == false`; uncertainties
     → `IsAsciiFresh == false`), so a `0.0` value never reads as "exactly
     zero".
   - **INS Status** - Mode (text), raw hex, IsGpsFix, IsGpsHeadingIns,
     IsGpsCompassActive, IsImuError, IsMagnetometerError, IsGnssError.
     Shared field - always populated if any source is fresh.
   - **Time Status** - raw hex + IsTimeOK / IsDateOK / IsUtcTimeValid +
     composite Valid. Whole section dashes out when `IsBinaryFresh ==
     false` (TimeStatus byte exists only in Binary subscription).
   - **Packet stats** - total received (sum), per-source rate · count
     ("ASCII: 4 Hz · 24 pkts" / "Binary: 100 Hz · 612 pkts"), last
     packet age (newest timestamp across both rings), source mode
     ("ASCII + Binary" / "ASCII only" / "Binary only" / "-").
5. **Status tracker note** - pane-header chip was attempted (chip next
   to selected device name) but tripped a hard `XamlCompiler` failure
   when bound via `{x:Bind ViewModel.SelectedDevice.ModeSeverity}` from
   the root page context (outside any `DataTemplate`); same chip in
   the card-row DataTemplate works. Card chip ships; pane-header chip
   deferred.

**Definition of done - verified against build:**
- All files compile (`dotnet build` succeeds, only the AnyCPU packaging
  warning remains which VS configurations don't hit).
- Hardware verification deferred to Phase 7.

---

### Phase 7 - Real hardware bring-up

**Goal:** Validate end-to-end against a real VN310 unit.

This phase is mostly manual / iterative - not many code changes anticipated,
but parser corrections are likely.

**Checklist:**
- [ ] Connect actual VN310 hardware.
- [ ] Verify packets are received and recognized (binary or ASCII).
- [ ] Verify all fields populate to plausible values when stationary at
      known location.
- [ ] Verify InsStatus decodes correctly (it should show Aligning during
      warm-up, then Tracking).
- [ ] Verify TimeStatus eventually becomes valid (IsTimeOK, IsDateOK,
      IsUtcTimeValid all true).
- [ ] Verify watchdog: power off the unit → device goes Error within 2s.
- [ ] Verify reconnect after power cycle works.
- [ ] Verify integration grid rows update at expected rate.
- [ ] Verify recording captures VN310 selections correctly (use
      RecordDecoderPro to verify).

**Definition of done:**
- VN310 produces correct, stable telemetry in the integration grid.
- All status indicators reflect reality.
- A recorded `.dat` decoded via RecordDecoderPro shows the expected values.

**Likely code touch-ups during this phase** (anticipate, but only fix what
you actually observe):
- Field extraction order in binary parser if the factory config differs
  from what we assumed.
- Yaw wrap-around edge cases (0/360 boundary).
- ASCII mode detection if the factory shipped VNINS instead of binary.
- Watchdog timeout tuning if 2s is too tight at the actual packet rate.
- **Binary group expansion** - Phase 2 deliberately subscribed only to
  `CommonGroup` + `TimeGroup`. The VN can also emit `AttitudeGroup` (YprU
  uncertainty, body-frame accels, quaternions), `InsGroup` (PosU/VelU
  uncertainty, ECEF variants), `ImuGroup` (raw IMU + temp + pressure),
  `GpsGroup` (fix info, DOP, sat counts). Decision deferred to here so we
  match whatever the factory is actually configured to emit. To add a
  group: extend `s_ExpectedCommonGroup` (or equivalent), append the new
  fields to the binary extraction order in `Vn310TelemetryService.ParseBinary`
  in the exact order the VN serializes them, and add the corresponding
  properties to `Vn310Telemetry`. ASCII path already captures everything
  VNINS carries (including the three uncertainties) - no change needed there.

---

## Implementation reference - cross-cutting behaviors

Load-bearing details that emerged during implementation and span multiple phases.
Captured here (rather than retro-fitted into the original phase sections) so the
"what we planned" history stays intact above, and a future TMAPS100 plan author
has one place to mirror these patterns.

### Device lifecycle & threading

- **First-packet handshake gate** (`Vn310TelemetryService` -
  `c_InitialPacketTimeoutMs = 3000`, `m_FirstPacketTcs`):
  `StartAsync` does NOT return success on SDK `Connect` alone. It awaits the
  arrival of the first packet (of any source) via a `TaskCompletionSource`
  signaled from `OnAsyncPacketReceived`. If no packet arrives within 3s, throws
  `TimeoutException` → caller maps to `DeviceStatus.Error` (Connecting→Error,
  NOT Connected→Error). Without this, SDK reports `IsConnected=true` on a
  wired-but-dead port and we'd loop forever in Connected → watchdog-fired
  Error → auto-reconnect → Connected → watchdog-fired Error …
- **Per-connect cancellation token** (`Vn310InsDevice` - `m_ConnectCts`,
  `m_CtsLock`): each `OnConnectAsync` mints a fresh CTS; `OnDisconnectAsync`
  cancels and disposes it. Lets a user-initiated Disconnect short-circuit
  the SDK's ~1s post-Connect settle delay (otherwise Disconnect would block
  until SDK finishes settling).
- **Stall handler teardown** (`Vn310InsDevice.OnServiceStalled`): when
  watchdog raises `Stalled`, the handler mirrors `OnDisconnectAsync` exactly
  - unsubscribe TelemetryUpdated/Stalled, cancel `m_ConnectCts`, fire-and-forget
  `m_Service.StopAsync()` - then `SetStatus(Error, ...)` (NOT Disconnected,
  so UI surfaces *why* the device went away). Without this teardown, the
  service stays `IsConnected=true` and the next Retry hits StartAsync's
  "already started" guard.
- **Mode-change debouncing** (`Vn310InsDevice` - `m_LastEmittedMode`,
  `m_CurrentMode`, `m_ModeLock`): the underlying `Vn310InsMode` is compared
  on every packet, but `ModeChanged` fires only on actual transitions.
  Without this, at 100Hz Binary the mode badge would re-render every 10ms.
- **Dispose with bounded wait** (`Vn310TelemetryService.Dispose` -
  `c_DisposeTimeoutMs = 2000`): `Dispose` calls `StopAsyncSafe` and waits
  up to 2s for the SDK to release the OS serial-port handle, so re-launching
  the app immediately doesn't fail with "port busy". A hung SDK can't block
  shutdown forever (2s cap).

### Hot-path threading & performance

- **Volatile.Read/Write contract** (`Vn310TelemetryService`,
  `Vn310SourceCandidateViewModel`): SDK packet thread writes the latest
  values via `Volatile.Write`; UI thread (250ms tick) + recording loop
  (100Hz, bypasses UI cadence) read via `Volatile.Read`. NO `PropertyChanged`
  ever fires from the packet thread - WinUI bindings throw on cross-thread
  notifications. The 250ms UI tick is what surfaces fresh values to bindings.
- **Static cached field extractors**
  (`Vn310SourceCandidateViewModel.s_FieldExtractors`): a `Dictionary<string,
  Func<Vn310Telemetry, double>>` of cached lambdas, allocation-free per
  packet after first reference. Matters because the SDK packet thread
  invokes the extractor on every packet (up to 104/s in dual-stream).

### Logging discipline

- **Rate-limited "incompatible binary packet" warning**
  (`Vn310TelemetryService.LogIncompatibleBinaryPacketRateLimited` -
  `c_IncompatibleLogIntervalSec = 60`, `m_LastIncompatibleLogUtc`): if the
  sensor is configured (via factory tools) for a binary subscription that
  doesn't match `s_ExpectedCommonGroup` / `s_ExpectedTimeGroup`, the
  warning fires at most once per minute. Prevents log spam (the sensor
  may emit the wrong layout at 100+ Hz).
- **Lifecycle log lines only** - Connect / Disconnect / first-packet /
  watchdog stall / source-mode transitions. Per-packet logging is
  forbidden on the hot path; the user reads daily log files post-mortem.

### Settings pane (`Vn310SettingsView` + `Vn310SettingsPaneViewModel`)

- **COM port dropdown lifecycle**: `SerialPort.GetPortNames()` is refreshed
  on `Loaded` / `DataContextChanged` / explicit Refresh button. Saved-but-
  currently-missing ports DO NOT get injected into the dropdown list
  (would let the user keep selecting a phantom port). Instead, a
  `SavedPortMissingMessage` renders below the dropdown explaining "saved
  port COMx not detected". `SyncComPortSelection()` forces `SelectedItem=
  null` when the saved port isn't in the live list, otherwise WinUI's
  default would show the first item visually while the bound value
  stayed at the saved string.
- **Baud rate ComboBox**: bound to `ViewModel.SerialBaudRates`
  (300/600/1200/2400/4800/9600/19200/38400/57600/115200/230400/460800/921600).
  Both `SerialBaudRate` (numeric) and `SerialBaudRateText` (string) setters
  cross-sync so dirty-detection works whether the dropdown or a (future)
  text input writes.

### Inspect pane (`Vn310InspectPaneViewModel`)

- **250ms standing refresh timer** (`c_RefreshIntervalMs = 250`,
  `DispatcherTimer m_RefreshTimer`): runs independent of incoming
  telemetry. Advances "last packet age" between packets and refreshes
  per-source Hz counters even when one source has gone silent - so the UI
  shows "ASCII: 0 Hz" rather than freezing at the last computed value.
- **`m_HasAnyPacket` guard on banner visibility**: the three source-mode
  banners (Dual / AsciiOnly / BinaryOnly) suppress until the first packet
  has been received. Without this guard, the AsciiOnly banner would flash
  on every device-Connect transition before any data has arrived.

### Convention for adding a new device

When porting these patterns to TMAPS100 (or any future INS):

1. Decide your transport. VN310 is serial-only via VectorNav SDK; TMAPS uses
   a different wire format.
2. Decide your async API. VN310 uses an SDK-provided `AsyncPacketReceived`
   event on a vendor-owned thread; if TMAPS streams over a UDP socket or
   custom serial protocol, you own the read loop and should marshal the
   same way (single-writer for per-source state, `Volatile.Write` for
   published snapshots).
3. Mirror the lifecycle gates: first-packet handshake, per-connect CTS,
   stall handler that fully tears down, Dispose with bounded port-release
   wait. These are universally load-bearing for any real device.
4. Keep `PropertyChanged` off the hot path. UI tick at 250ms; recording
   reads directly via Volatile.
5. Add per-source plumbing ONLY if the device can emit multiple wire
   formats concurrently. If it can't (single binary stream), skip the
   merge layer - a single-source path is simpler and there's no benefit
   to over-generalizing.

---

## Status tracker

Append updates here as phases complete, so future-you knows where to pick up.

- [x] Phase 1 - Library bring-up (2026-05-20; vendored DLL path taken - `VectorNav` not on nuget.org)
- [x] Phase 2 - Telemetry service + parsing (2026-05-20; smoke-tested against COM_NOT_REAL - `System.IO.FileNotFoundException` surfaces cleanly through `StartAsync`)
- [x] Phase 3 - Wire `Vn310InsDevice` to the service (2026-05-20; bad-port path verified via UI → Error status with friendly message; SDK-originated `FileNotFoundException` shielded behind a `SerialPort.GetPortNames()` pre-flight to avoid VS first-chance breaks)
- [x] Phase 4 - Integration grid candidate (2026-05-20; bad-port path re-verified, column-lifecycle deferred to Phase 7 hardware bring-up)
- [x] Phase 5 - Connection settings UI + RecommendedHint (2026-05-24; VN310-specific settings pane w/ COM-port dropdown + Refresh + stale-port warning; card UX: border-tint-on-status, error-row icon + red text, "Retry" button text in Error state; fixed UI refresh on background StateChanged via DispatcherQueue, fixed stall-handler not stopping service)
- [x] Phase 6 - Status surfaces (2026-05-24; mode chip on card via `DeviceModeChip` + IInsDevice.CurrentMode/ModeChanged contract; inspect refactored to base+factory+selector mirroring Phase 5.5; `Vn310InspectPaneViewModel` + `Vn310InspectView` with telemetry / INS Status / Time Status / packet stats sections + ASCII-vs-Binary asymmetry banners; packet stats live on `Vn310TelemetryService` so they survive pane open/close. Pane-header chip deferred - root-page `x:Bind` to ModeSeverity tripped XamlCompiler; card chip ships)
- [x] Phase 8 - Dual-stream ASCII + Binary support (2026-05-26)
  - **Trigger:** user configured the sensor (via VectorNav Control Center) to
    emit ASCII VNINS @ 4 Hz *and* Binary @ 100 Hz concurrently. Original
    snapshot-per-packet design caused every field to oscillate every tick:
    ASCII packets zeroed the rates / TimeStatus (because ASCII parser sets
    them to zero); Binary packets zeroed the uncertainties.
  - **Architecture:** per-source latest snapshots (`m_LatestAscii`,
    `m_LatestBinary`) + a merge function that composes the published
    `Vn310Telemetry` on every packet. Shared fields → freshest source.
    Binary-exclusive → `m_LatestBinary` if fresh, else zero. ASCII-exclusive
    → `m_LatestAscii` if fresh, else zero. Per-source staleness window: 2s
    (matches main watchdog). UtcTime: prefer Binary's raw Y/M/D over
    ASCII's leap-hack reconstruction when both are fresh.
  - **Contract changes:**
    - `Vn310Telemetry.HasRates` removed. Replaced with `IsAsciiFresh` +
      `IsBinaryFresh` - per-source freshness flags computed at merge time.
    - `Vn310PacketSourceMode` enum redefined from `Unknown/Ascii/Binary`
      (last-packet kind) to `Unknown/AsciiOnly/BinaryOnly/Both`
      (currently-fresh composition).
    - Per-source stats: `AsciiPacketCount` / `BinaryPacketCount` /
      `GetRecentAsciiTimestamps()` / `GetRecentBinaryTimestamps()`.
      Aggregate `PacketCount` retained as the sum.
  - **UI:** three banners in the inspect view (dual / ascii-only /
    binary-only) replacing the previous two; packet-stats section gains
    per-source rate · count lines.
  - **Logging:** lifecycle-only transition messages (Unknown→Both, Both→
    BinaryOnly when ASCII stalls, recovery, etc.). No per-packet noise.
  - **Edge cases verified by design (not hardware-tested yet):** legacy
    ASCII-only and Binary-only sensors degrade to the old behavior
    (missing source's freshness flag stays false, exclusive fields stay
    zeroed); mid-session single-source loss flips the corresponding flag
    after 2s, keeps the other source flowing, logs the transition; both
    sources stalling triggers the main watchdog at 2s as before.
- [~] Phase 7 - Real hardware bring-up (2026-05-25, partial)
  - **Verified:** packets received & recognized (ASCII VNINS at 4–5 Hz); YPR plausible immediately via AHRS; InsStatus bit decoder confirmed 1:1 with reference at `Navigation-LEAN/.../VN310_Statusses.cs`; after sky view + dual antennas + several-minute wait, unit reached `Tracking` and all INS-derived fields populated correctly; integration grid rows update at sensor rate; recording captures VN310 selections (decoded round-trip via RecordDecoderPro); USB unplug → clean disconnect via SDK's `userUnpluggedUsbCable` path → AutoReconnect resumes when re-plugged.
  - **Known issues / deferred:**
    - **Output rate stuck at 4–5 Hz** across three independent softwares - likely sensor's `AsyncDataOutputFrequency` reconfigured low at some point. Confirmed not a NIS bug. Deferred to senior-engineer conversation; fix via VectorNav Sensor Explorer (Register 7) outside of NIS.
    - **Power-loss while connected crashes the app** - VectorNav SDK throws on its own background thread (`SerialPort.cs:290`). Cannot be caught from NIS code (thread we don't own, .NET 8 terminates on unhandled). See `MIGRATION_NOTES.md` section 7. Mitigation shipped: global `AppDomain`/`Application`/`TaskScheduler` unhandled-exception handlers in `App.xaml.cs` log full stack + thread name (`VN.SerialPort (COMx)`) before death, so post-mortem is possible. True fix (SDK fork) deferred.
    - **TimeStatus validation** in ASCII mode - N/A (TimeStatus byte exists only in Binary subscription). UTC time itself populates fine via the VNINS time field + leap-second offset.
  - **Side improvements landed:**
    - Baud Rate input upgraded from free-text TextBox to ComboBox (general industry rates 300…921600) in both `Vn310SettingsView` and `RealDeviceSettingsView`. `SerialBaudRate` setter now syncs `SerialBaudRateText` so dirty-detection stays accurate whichever control writes.

---

## Open questions deferred to implementation time

None blocking. Phase 1 may reveal that NuGet doesn't load cleanly on .NET 8 -
fall back to vendored DLL per Phase 1 step 3.

If Phase 7 reveals the unit is shipping ASCII not binary, decide whether to
ask the factory to reconfigure (one-time, not at NIS connect-time) or accept
the no-rates limitation.

---

*Last updated 2026-05-26 - Phase 8 (dual-stream ASCII + Binary) appended.*
