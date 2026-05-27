# Recording, Playback, and Emulation Refactor Plan

> **For Claude resuming this work after `/clear`:** read this file end-to-end
> before touching code. It captures every decision and the reasoning behind it
> so you don't have to re-derive anything from chat history. This plan is the
> result of a brainstorming session on 2026-05-27. Companion file:
> `VN310_PLAN.md` (separate scope, already in flight).

---

## Goal (one sentence)

Replace NIS's current single-purpose recording pipeline with a unified data
lifecycle: raw inbound packets per device + an integrated snapshot stream, both
written to one `.dat` file, with the integrated stream optionally also
transmitted live over UDP under a configurable "emulation persona" (acting as
the integrated INS itself or impersonating a single specific INS like VN310),
plus a dedicated playback page that decodes recordings and provides
device-comparison graphs.

---

## Glossary - what code lives where

So future-you doesn't conflate sources:

- **NIS** - this repo (`C:\Users\BARBIC\source\repos\NavigationIntegrationSystem`).
- **Parent solution** - the eventual umbrella that NIS will merge into.
  `Infrastructure/TO_BE_DELETED/` holds files vendored from the parent that
  NIS depends on temporarily; they get deleted on merge.
- **g2-master** - reference codebase at
  `C:\Users\BARBIC\Desktop\Work\NavigationControlSystem\navigationsystemscontroller-g2-master\`.
  Canonical "current state" of the parent's RecordDecoderPro and pre-NIS
  navigation controller. Used as authoritative when checking
  `DataRecordType` enum, header layouts, `ItemTemplate` parsers.
- **Navigation-LEAN** - older copy of the same parent family at
  `C:\Users\BARBIC\Desktop\Work\NavigationControlSystem\Navigation-LEAN\`.
  Content-identical to g2-master for the files we care about. Used only when
  g2-master is missing a file.
- **PARENT_PATCHES/** (new, this plan introduces) - NIS-authored files that
  REPLACE/ADD into the parent solution at merge time. The opposite of
  `TO_BE_DELETED/`. Each file is paired with a destination path and an
  action (REPLACE / ADD / DELETE).

---

## Locked Design Decisions (do not re-litigate)

Every decision below was settled in the 2026-05-27 brainstorming session.

### 1. Yaw rename across both sides

- NIS-owned code: rename every `Azimuth*` to `Yaw*` (field names, status flag
  names, constants, method params, MIGRATION_NOTES, this file, memory files).
- Parent solution: the user will also move the parent side to Yaw on their
  side. NIS-authored renamed versions of parent-vendored types ship via
  `PARENT_PATCHES/` for the user to apply.
- Affected types moving OUT of `TO_BE_DELETED/` (because we're authoring new
  versions to ship to the parent):
  - `IntegratedInsOutput_Data.cs`
  - `IntegratedInsOutput_CommFrame.cs` (if we keep it; see decision 5)
  - `IntegratedInsOutputStatusFlags.cs`
- Files staying in `TO_BE_DELETED/` (untouched): everything else. The
  `WGS84Data`, `EulerData`, `NEDData`, etc. that the renamed types depend on
  stay vendored.
- **Why:** the old "keep Azimuth on the NIS side to match parent's binary
  schema" trade-off was unnecessary tech debt. The parent is moving to Yaw
  anyway. Doing it once, in lockstep, is cleaner than maintaining a translation
  layer at the `IntegrationSnapshotService` write site.

### 2. Wire format and header (unchanged from parent)

Per-record on-disk layout, every record:

```
+----+----+------+------+------+--------------+
|Sync| ID | Len  | Time | Type | Data         |
| 2  | 2  |  2   |  8   |  2   | <Len> bytes  |
+----+----+------+------+------+--------------+
```

- `Sync` = `0x7E55` always. Acts as resync marker; reader scans for it if
  drifted.
- `ID` = ushort. **Repurposed as device instance ID** (0 for first VN310, 1
  for second, etc.). Currently unused by every parser. RecordDecoderPro
  patches must display this as a column.
- `Len` = byte count of `Data` (excluding header).
- `Time` = `DateTime.ToBinary()` of write time.
- `Type` = `DataRecordType` enum value (parent's `Infrastructure.Enums.DataRecordID.cs`).
- `Data` = type-specific payload, opaque to the framing layer.

Header total = 16 bytes. Source of truth: g2-master `BinaryFileRecorder.cs`
(commented-out reference) + `DataRecordHeader.cs` + RecordDecoderPro
`MainWindow.xaml.cs:124-140`.

### 3. Record types (reuse existing enum values)

Existing `DataRecordType` enum values used by this plan:

| Value (decimal) | Enum name | Producer | Schema |
|---|---|---|---|
| 30 | `VN310_INS` | Per-packet from `Vn310TelemetryService` | NEW: `Vn310InsRecord` struct (this plan) |
| 31 | `Tmaps100_INS` | Per-packet from `Tmaps100xTelemetryService` (future) | NEW: `Tmaps100xInsRecord` struct |
| 50 | `IntegratedInsOutputRawData` | Every tick of `IntegratedOutputProducer` | `IntegratedInsOutput_Data` (existing, cleaned up) |

**No new enum values for now.** If a future SDK adds fields incompatible with
the current schema, allocate a new enum value (e.g., `VN310_INS_V2 = 60`); do
NOT mutate an existing one. The ushort enum has ~65k values, ~30 used.

**Versioning byte rejected.** Producer can identify schema version via
`DataRecordType` alone. Simpler than branching inside parsers.

### 4. Per-INS payload shape: structured snapshots (Path 3 from brainstorm)

For VN310 and TMAPS raw records, the payload is a **NIS-defined binary struct**
parsed from the device's wire protocol, NOT the raw wire bytes.

**Why structured over raw bytes:**
- Comparison graphs in the playback page need same-named fields across
  devices (yaw, pitch, ...). Raw-bytes would require re-parsing wire protocols
  at decode time, per axis.
- The parent's `RecordDecoderPro` no longer needs a VectorNav SDK dependency
  (currently `VN310Item.cs` uses `VectorNav.Protocol.Uart.Packet`). One fewer
  vendor dependency in the parent.
- Adding a future INS = define a struct, no protocol parser to ship in
  RecordDecoderPro.

**Trade-off accepted:** if NIS's parser had a bug at record time, the recorded
struct reflects the bug. No re-parsing old data with new logic. Forensic
re-decode is rare in practice; the parser ships with the NIS version that did
the recording.

### 5. `IntegratedInsOutput_Data` cleanups (keep schema, rewrite internals)

Schema (per-field `{DeviceCode, DeviceId, Value}` triplets + status bitmask)
is correct. Internals need cleanup:

- **Drop the internal lock.** Lock is half-applied (compound fields lock,
  scalar Code/Id fields don't) so it provides neither atomicity nor
  consistency. Treat as plain DTO. Snapshot semantics live at the producer
  (clone once on tick, hand off, no further mutation).
- **Drop the clone-on-accessor pattern** on `Position`, `EulerData`,
  `VelocityVector`. Once the lock is gone, accessors can return the actual
  instance.
- **Drop stored `VelocityTotal`** from the payload. Derivable from N/E/D. 8
  bytes per record saved. RecordDecoderPro computes on read.
- **Fix `Clear()`:** replace `m_OutputTime = DateTime.UtcNow` with
  `m_OutputTime = DateTime.MinValue`. Makes "never populated" distinguishable
  from "populated 1ms ago" and makes `Clear()` idempotent.
- **Keep `OutputTime` in payload** even though `header.Time` also exists.
  Semantically different (`header.Time` = file-write time; `OutputTime` =
  decision time). RecordDecoderPro already emits both as separate CSV
  columns. Useful for analyzing slack.
- **Status triplet:** `StatusValue` keeps Code/Id (always 0 since status is
  computed, not contributed). Mildly cargo-culted but harmless.

**`IntegratedInsOutput_CommFrame`:** keep the existing CommFrame envelope
(`[0x50 sync][Data][checksum]`) since RecordDecoderPro's `IntegratedInsOutputItem`
uses it. Decision: don't strip the wrapper, just clean up the inner Data.

### 6. Producer/consumer architecture

```
                          IntegratedOutputProducer
                          (timer-driven, configurable Hz,
                           publishes "latest snapshot" via
                           volatile field + SnapshotProduced event)
                                       |
                                       | event SnapshotProduced(IntegratedInsOutput_Data)
                                       |
              +------------------------+------------------------+
              |                                                 |
              v                                                 v
       IRecordingService                              IEmulationModeSender
       (subscribes when recording                     (one active at a time,
        starts; writes type=50                         picks ticks at its
        records via the same                           own rate; sends UDP
        funnel as per-device                           via BaseUdpTransmitter)
        raw writers)
```

**Reference-counted producer lifecycle:** producer starts when first
subscriber attaches, stops when last detaches. Avoids running the loop when
neither recording nor any sender wants it.

**One funnel for ALL writes** (`IRecordingService.WriteRecord(type, instanceId, time, payload)`):
- Per-device raw writers (event-driven from `*TelemetryService.TelemetryUpdated`)
  go through it.
- The producer-driven integrated writer also goes through it.
- Single lock around the FileStream, deterministic ordering, one place to add
  CRC if we ever want it.

### 7. EmulationMode and senders

```csharp
public enum EmulationMode
{
    None,        // outbound silent
    Integrated,  // NIS native integrated format, target = VIC
    Vn310,       // impersonates VN310 ASCII VNINS, target = ACU / AL-4000
    // Tmaps100x, // future: impersonate TMAPS
}

public interface IEmulationModeSender
{
    EmulationMode Mode { get; }
    int RateHz { get; }            // configurable per sender
    UdpEndpoint Endpoint { get; }  // {Ip, Port}
    void Start();                  // owns its own timer + UDP socket
    void Stop();
    bool IsRunning { get; }
}

public sealed class IntegratedEmulationSender : IEmulationModeSender { ... }
public sealed class Vn310EmulationSender : IEmulationModeSender { ... }

public sealed class EmulationModeService
{
    EmulationMode CurrentMode { get; }
    void SwitchTo(EmulationMode mode);    // stops current, starts new (exclusive)
    IReadOnlyList<IEmulationModeSender> AvailableSenders { get; }
}
```

**Locked rules:**
- **Exclusive selection** (one mode at a time). UI is a dropdown, not toggles.
  Architecture allows parallel later (change `CurrentMode` to `ActiveModes` set)
  but ships exclusive.
- **Per-mode endpoint persistence.** Switching modes auto-loads that mode's
  `{Ip, Port}`. Settings model:
  `EmulationSettings { CurrentMode, TickRateHz, EndpointsByMode: Dict<EmulationMode, UdpEndpoint> }`.
- **Recording and EmulationMode are orthogonal.** Any combination valid:
  recording on + EmulationMode=None, recording off + EmulationMode=Vn310, etc.
- **NIS-as-Vn310 reports its OWN instance ID**, not a real connected VN310's.
  Configurable in settings ("Reported InstanceId = 0"). Consumer sees one
  VN310, never two.

**Why "EmulationMode" naming:** describes what NIS appears AS, not who it
sends TO. "Integrated" value = NIS in its native form. "Vn310" value = NIS
wearing a VN310 costume. Matches existing `Integrated*` naming convention in
the codebase.

### 8. Hz tick rate setting

- User-typed integer in the Settings page, free text with validation rule:
  `1 <= N <= 1000 AND 1000 mod N == 0`. So valid: 1, 2, 4, 5, 8, 10, 20, 25,
  40, 50, 100, 125, 200, 250, 500, 1000.
- **One Hz setting drives both recording and the active sender.** They run
  on the same producer tick.
- Per-mode recommended hints via the existing `RecommendedHint` control:
  - `Integrated` -> recommended 100 Hz
  - `Vn310` -> recommended 40 Hz (matches typical real-VN310 default)
  - `None` -> N/A (Hz still applies if recording is on)
- **Why:** centralizing the rate means "what we record == what we emit",
  per user's guideline. Avoids the trap of recording at one cadence and
  emitting at another and having to reconcile.

### 9. Settings page UI

- Use the existing currently-empty Settings page (top-level nav target).
- Sections to add:
  - **Output / Emulation**:
    - Dropdown: `EmulationMode` (None | Integrated | Vn310)
    - Textbox: `Tick Rate (Hz)` with the divides-1000 validator + recommended-hint
    - Textbox: `Endpoint IP` (per-mode, switches when mode changes)
    - Textbox: `Endpoint Port` (per-mode)
    - Textbox: `Reported InstanceId` (only when mode = Vn310)
    - Button: Start / Stop
    - Read-only: status indicator (Running / Stopped / Error + last-send time)

Other Settings sections (logging, app prefs, etc.) can be added later by
other plans; this plan only adds Output/Emulation.

### 10. Playback page design

- Top-level navigation target ("Playback"), alongside Devices / Integration /
  Settings.
- **Two streams parsed from one `.dat` file on load:**
  - `List<IntegratedRecord>` ordered by time. Drives current-frame cursor,
    provenance grid, UTC clock, transport.
  - `Dictionary<(DeviceType, InstanceId), List<RawRecord>>`. Each list ordered
    by time. Feeds comparison graphs as full traces.
- **Layout (rough):**
  ```
  +------------------------------------------------+
  |  Playback File: [browse]  recording_xyz.dat    |
  |  UTC: 2026-05-27 14:23:01.234                  |
  +------------------------------------------------+
  |  Transport: [<<] [play/pause] [stop]           |
  |  Position: |======*-------------|  12:34/45:00 |
  |  Speed: [0.5x] [1x] [2x] [5x]                  |
  +-----------------------------+------------------+
  | Provenance Grid             | Comparison Graphs|
  | (current frame)             |                  |
  |                             |  [ Yaw      \/ ] |
  | Field | Device | Id | Value | +--------------+ |
  | Lat   | VN310  | 0  | 32.1  | | line per dev | |
  | Lon   | TMAPS  | 0  | 34.8  | |    cursor    | |
  | Alt   | VN310  | 0  | 235.4 | |              | |
  | Yaw   | VN310  | 1  | 122.3 | +--------------+ |
  | ...                          |  [ Pitch    \/ ] |
  +-----------------------------+--+--------------+|
  ```
- **Provenance grid** = one row per integration field, three columns
  (Device, Id, Value) showing what the integrated decision was at the
  current frame's `OutputTime`.
- **Comparison graphs** = one panel per selectable field (Yaw, Pitch, Roll,
  Lat, Lon, Alt, YawRate, PitchRate, RollRate, VelN, VelE, VelD, Course,
  VelocityTotal). One line per recorded `(DeviceType, InstanceId)` that
  contributed values for that field. Vertical cursor synced to the transport
  position.

**Transport speed multipliers** (0.5x, 1x, 2x, 5x): in scope for v1. Useful
for forensic review.

### 11. Comparison graphs via ScottPlot.WinUI

- Library: **ScottPlot.WinUI** (MIT, pure managed + SkiaSharp native, offline-safe).
- Verified: no telemetry, no license-server, no runtime internet. NuGet
  packages restore once via `dotnet restore` on the online dev PC, then
  `.nuget/packages` ships to the offline target PC.
- Why ScottPlot over alternatives:
  - Specifically tuned for high-density time-series. Built-in decimation
    handles 360k+ point traces (1 hour at 100 Hz) without UI lag.
  - WinUI 3 native control (`WinUIPlot`). No interop layer.
  - Simpler API than OxyPlot, fewer dependencies than LiveCharts.
- Each field gets its own `WinUIPlot`. Series colors derived from a stable
  palette keyed by `(DeviceType, InstanceId)` so the same device is the same
  color across all graphs.

### 12. RecordDecoderPro PARENT_PATCHES strategy

- All NIS-authored changes for RecordDecoderPro live under
  `src/NavigationIntegrationSystem.Infrastructure/PARENT_PATCHES/`.
- Mirror tree mirrors the parent's source layout, so the user can copy a
  whole subtree directly.
- `_README.md` at the root of `PARENT_PATCHES/` lists every file inside with
  three columns: source path (NIS), destination path (parent), action
  (REPLACE / ADD / DELETE).
- **Do NOT refactor the central switch in RecordDecoderPro's MainWindow.xaml.cs.**
  Other ItemTemplates (Tvt, GTV, CTC, Fox, Orbit, Inp*, Neighbors*, C4*, etc.)
  are out of scope. Only modify:
  - The three NIS-related cases: `VN310_INS`, `Tmaps100_INS`,
    `IntegratedInsOutputRawData` (add new case).
  - The throwing `Enum.IsDefined` check at line 142: replace with log+skip
    so future record types don't kill decoding.
- Concrete deliverables in `PARENT_PATCHES/`:
  - `RecordDecoderPro/ItemTemplates/VN310Item.cs` (REPLACE)
  - `RecordDecoderPro/ItemTemplates/TmapsItem.cs` (REPLACE)
  - `RecordDecoderPro/ItemTemplates/IntegratedInsOutputItem.cs` (REPLACE - parent has it but we changed the data schema)
  - `RecordDecoderPro/MainWindow.xaml.cs.patch` (text diff against g2-master's; surgical edits only)
  - `Infrastructure/Enums/DataRecordID.cs` (REPLACE if Yaw rename touches any enum value names; otherwise omit)
  - `Infrastructure/Navigation/NavigationSystems/IntegratedInsOutput/IntegratedInsOutput_Data.cs` (REPLACE - cleaned-up version)
  - `Infrastructure/Navigation/NavigationSystems/IntegratedInsOutput/IntegratedInsOutputStatusFlags.cs` (REPLACE - Yaw rename)
  - `Infrastructure/Navigation/NavigationSystems/IntegratedInsOutput/IntegratedInsOutput_CommFrame.cs` (REPLACE if internals changed)
  - For VN310 raw and TMAPS raw payload structs: NEW files at
    `Infrastructure/Navigation/NavigationSystems/Records/Vn310InsRecord.cs`
    and `.../Tmaps100xInsRecord.cs` (ADD, since parent's existing parsers used
    raw wire bytes and don't have these structs).

### 13. TO_BE_DELETED migration

After this refactor:

- **Files leaving TO_BE_DELETED** (NIS becomes the authoritative author,
  delivered to parent via PARENT_PATCHES):
  - `IntegratedInsOutput_Data.cs`
  - `IntegratedInsOutput_CommFrame.cs`
  - `IntegratedInsOutputStatusFlags.cs`
  - `IntegratedInsOutputItem.cs` (RecordDecoderPro side; moves to PARENT_PATCHES, deleted from NIS)
- **Files staying in TO_BE_DELETED** (still vendored from parent, untouched):
  - `WGS84Data.cs`
  - `EulerData.cs`
  - `NEDData.cs`
  - `IntegratedValueTriplet.cs` (if present)
  - All other supporting types

The renamed/cleaned-up types that leave TO_BE_DELETED move to a new NIS
location: `src/NavigationIntegrationSystem.Infrastructure/Recording/Payloads/`.
This makes their NIS-authored status visible by location.

### 14. CSV deletion

- `CsvTestingService.cs` deleted (this is the WRITER side; sidecar CSV during recording).
- `IntegrationSnapshotService.m_CsvTester` field removed.
- DI registration for `CsvTestingService` removed.
- `Recordings/TestLog_*.csv` files left on disk untouched (user can clean
  manually); no migration script.

### 15. PlaybackInsDevice + CsvPlaybackService (kept, parallel feature)

`PlaybackInsDevice` (a device kind selectable in the integration grid) and
its backing `CsvPlaybackService` (the READER side, loads user-supplied CSV
files) remain a first-class feature, untouched by this plan. They serve a
different use case from the new top-level Playback page:

- **CSV-into-integration path (kept as-is):** user authors a CSV (hand-rolled
  for testing, exported from another tool, etc.), loads it via the Playback
  device card, selects "Playback" as the source for one or more rows in the
  integration grid. Useful for: replaying synthetic scenarios, mixing
  recorded values with live devices, ad-hoc test inputs.
- **.dat forensic-review path (NEW in this plan):** user opens a NIS-produced
  .dat in the new top-level Playback page for provenance grid + comparison
  graphs. Read-only review of a real recorded session.

The two paths are independent. CSV input has nothing to do with .dat
recording output. `CsvPlaybackService` keeps reading external CSVs;
`CsvTestingService` (the WRITER side, decision 14) is the only CSV component
deleted.

**Implication:** the "Files Staged for Deletion" understanding from earlier
project memory does NOT include `PlaybackInsDevice` or `CsvPlaybackService`.
They are permanent NIS code, not legacy.

Do NOT touch these classes in Phases 0-5.

---

## Bugs to fix during refactor

Consolidated from the brainstorm. Each gets handled in the phase that touches
the affected file.

### In parent code (via PARENT_PATCHES):

1. `VN310Item.cs`: binary VNINS path commented out. With structured-snapshot
   refactor this becomes moot (we send the parsed struct; parser doesn't
   branch on ASCII vs Binary).
2. `VN310Item.cs`: ASCII rates silently zeroed. Replace zero-fill with a
   "rate not available" marker (sentinel value like `double.NaN`, or a
   separate `HasRates` flag in the new struct).
3. `VN310Item.cs` / `TmapsItem.cs`: typo `AngelsYaw` -> `AnglesYaw` (will be
   `YawAngles` post-rename, but spell it right).
4. `VN310Item.cs`: hardcoded `(time - 18) % 86400` leap-second offset. With
   structured snapshots the leap-second hack only lives in NIS's
   `Vn310TelemetryService` (one place), not duplicated in RecordDecoderPro.
5. `MainWindow.xaml.cs:142-144`: `Enum.IsDefined` throw on unknown DataType
   kills decoding of the whole file. Replace with log+skip.
6. `MainWindow.xaml.cs:353`: `Console.WriteLine` for errors in a WPF app
   goes to the void. Route to a log file or status panel. (If we don't want
   to add log4net wiring, write to stderr or a status TextBlock.)
7. `Tmaps100_CommFrame.DecodeBinaryData(rawData, header.DataLength)` -
   `rawData.Length == header.DataLength` by construction. Drop the redundant
   second argument (or fix at the new `Tmaps100xInsRecord.Deserialize` site
   so the pattern doesn't replicate).

### In NIS:

8. `IntegrationSnapshotService` runs the 100 Hz loop unconditionally; bails
   inside on `!IsRecording`. With producer/consumer refactor, the
   `IntegratedOutputProducer` only ticks when subscribed. Fixed structurally.
9. `IntegratedInsOutput_Data` cleanups per decision 5.

Each fix should be called out in the commit message of the phase that lands
it, so it shows up in `git log --grep`.

---

## File-by-file impact summary

### NIS - new files

```
src/NavigationIntegrationSystem.Infrastructure/Recording/
  IntegratedOutputProducer.cs               (replaces IntegrationSnapshotService internals)
  NisRecordingService.cs                    (already exists; refactored to take generic ISerializableRecord)
  Payloads/
    Vn310InsRecord.cs                       (NEW - wire payload, separate from in-memory Vn310Telemetry)
    Tmaps100xInsRecord.cs                   (NEW - future; stub in this plan)
    IntegratedInsOutput_Data.cs             (MOVED out of TO_BE_DELETED, cleaned up, Yaw rename)
    IntegratedInsOutput_CommFrame.cs        (MOVED out of TO_BE_DELETED, kept)
    IntegratedInsOutputStatusFlags.cs       (MOVED out of TO_BE_DELETED, Yaw rename)
  Writers/
    Vn310RawRecordWriter.cs                 (NEW - subscribes to TelemetryUpdated, maps to record, writes)
    Tmaps100xRawRecordWriter.cs             (NEW - future)
  Emulation/
    EmulationModeService.cs                 (NEW)
    EmulationSettings.cs                    (NEW)
    UdpEndpoint.cs                          (NEW)
    Senders/
      IEmulationModeSender.cs               (NEW interface)
      IntegratedEmulationSender.cs          (NEW)
      Vn310EmulationSender.cs               (NEW)
  PARENT_PATCHES/                           (NEW folder, NIS-authored, ships to parent at merge)
    _README.md
    RecordDecoderPro/
      ItemTemplates/
        VN310Item.cs                        (REPLACE)
        TmapsItem.cs                        (REPLACE)
        IntegratedInsOutputItem.cs          (REPLACE)
      MainWindow.xaml.cs.patch              (diff)
    Infrastructure/
      Enums/
        DataRecordID.cs                     (REPLACE if Yaw enum names changed)
      Navigation/NavigationSystems/IntegratedInsOutput/
        IntegratedInsOutput_Data.cs         (REPLACE - same content as in NIS Recording/Payloads/)
        IntegratedInsOutput_CommFrame.cs    (REPLACE)
        IntegratedInsOutputStatusFlags.cs   (REPLACE)
      Navigation/NavigationSystems/Records/
        Vn310InsRecord.cs                   (ADD - parent doesn't have this struct yet)
        Tmaps100xInsRecord.cs               (ADD)

src/NavigationIntegrationSystem.UI/
  Views/Pages/
    PlaybackPage.xaml(.cs)                  (NEW - top-level nav target)
    SettingsPage.xaml(.cs)                  (EXISTS but empty; add Output/Emulation section)
  Views/Controls/
    TransportControl.xaml(.cs)              (NEW - play/pause/stop/scrub/speed)
    ComparisonGraph.xaml(.cs)               (NEW - wraps ScottPlot WinUIPlot, multi-series)
  ViewModels/Playback/
    PlaybackPageViewModel.cs                (NEW)
    PlaybackFileReaderService.cs            (NEW - parses .dat into two stream collections)
  ViewModels/Settings/
    SettingsPageViewModel.cs                (NEW or already-stub)
    EmulationSettingsViewModel.cs           (NEW - section VM)
```

### NIS - files DELETED

```
src/NavigationIntegrationSystem.UI/Services/Recording/CsvTestingService.cs
src/NavigationIntegrationSystem.Infrastructure/TO_BE_DELETED/IntegratedInsOutput_Data.cs           (moved, not just deleted)
src/NavigationIntegrationSystem.Infrastructure/TO_BE_DELETED/IntegratedInsOutput_CommFrame.cs      (moved)
src/NavigationIntegrationSystem.Infrastructure/TO_BE_DELETED/IntegratedInsOutputStatusFlags.cs     (moved)
src/NavigationIntegrationSystem.Infrastructure/TO_BE_DELETED/IntegratedInsOutputItem.cs            (moves to PARENT_PATCHES)
```

### NIS - files MODIFIED

```
src/NavigationIntegrationSystem.Devices/Implementations/Vn310/Vn310TelemetryService.cs
   - add hook to Vn310RawRecordWriter via DI

src/NavigationIntegrationSystem.UI/Services/Recording/IntegrationSnapshotService.cs
   - rename/refactor into IntegratedOutputProducer
   - drop CsvTestingService dependency

src/NavigationIntegrationSystem.UI/Bootstrap/HostBuilderFactory.cs
   - register IntegratedOutputProducer, EmulationModeService, all senders, PlaybackFileReaderService
   - remove CsvTestingService registration

src/NavigationIntegrationSystem.UI/Views/MainWindow.xaml(.cs)
   - add "Playback" and "Settings" top-level nav entries (if not present)

MIGRATION_NOTES.md
   - section 2 (Yaw/Azimuth): rewrite with the Yaw migration plan
   - section 4 (Files / patterns superseded by parent equivalents): list moved-out-of-TO_BE_DELETED files
   - section 5 (Behavioral changes NIS introduces): add EmulationMode service, raw-per-device recording, structured payload schema, dropped CSV
   - new section: pointer to PARENT_PATCHES/_README.md

CLAUDE.md
   - update "Files Staged for Deletion" section if it references the moved files
   - update "What's Left" / "Current Devices" if relevant

VN310_PLAN.md
   - no changes (separate scope, this plan is independent)
```

---

## Phases

Each phase is a complete, mergeable unit. After each: app compiles, launches,
nothing previously working is broken. Phases ordered by dependency.

### Phase 0 - Yaw rename

**Goal:** rename every `Azimuth*` to `Yaw*` in NIS-owned code. Mechanical;
no behavior change. Sets the stage for all subsequent phases.

**Steps:**
1. Move `IntegratedInsOutput_Data.cs`, `IntegratedInsOutput_CommFrame.cs`,
   `IntegratedInsOutputStatusFlags.cs` from `Infrastructure/TO_BE_DELETED/`
   to `Infrastructure/Recording/Payloads/`. Namespace updates.
2. In those three files: rename `Azimuth*` -> `Yaw*` (every member, every flag,
   every comment). This includes `AzimuthDeviceCode` -> `YawDeviceCode`,
   `AzimuthRateDeviceCode` -> `YawRateDeviceCode`, `EulerAzimuth` -> `EulerYaw`,
   `AzimuthValid` -> `YawValid`, `AzimuthRateValid` -> `YawRateValid`.
3. Update all NIS callers to the renamed members:
   - `IntegrationSnapshotService.cs` (switch case bodies, status flag ORs)
   - `IntegrationFieldKeyMap.cs` if any value strings reference `Azimuth`
   - Any other grep hits for `Azimuth`
4. Author PARENT_PATCHES versions of the three moved files (identical content,
   in the parent's namespace).
5. Update `MIGRATION_NOTES.md` section 2 with the new Yaw migration plan.
6. `dotnet build` clean.

**Definition of done:**
- Repo grep for `Azimuth` returns zero hits in NIS-owned code (TO_BE_DELETED
  remnants OK if any unrelated types still reference it).
- App launches, integration grid still shows the "Yaw" row label (no UI change
  expected since the row label was already "Yaw").
- VN310 telemetry still feeds the integration grid correctly.
- PARENT_PATCHES has the three Yaw-renamed files ready for the user to apply.

**Don't do in this phase:** any architectural refactor, any new recording
behavior, any UI changes beyond what the rename forces.

---

### Phase 1 - Foundation refactor (producer/consumer, IntegratedInsOutput_Data cleanup, drop CSV)

**Goal:** introduce the producer/consumer pattern, clean up
`IntegratedInsOutput_Data`, drop `CsvTestingService`. End state: existing
recording behavior preserved but routed through the new architecture.

**Steps:**
1. Rename `IntegrationSnapshotService` -> `IntegratedOutputProducer`. Refactor
   internals:
   - Constructor takes `IOptions<EmulationSettings>` (or a settings provider)
     for `TickRateHz`.
   - `Timer` based on `1000 / TickRateHz` ms.
   - Reference-counted subscriber lifecycle: timer starts on first
     `Subscribe`, stops on last `Unsubscribe`.
   - Exposes `event EventHandler<IntegratedInsOutput_Data> SnapshotProduced`.
   - Exposes `volatile IntegratedInsOutput_Data? LatestSnapshot` for poll-style
     readers (senders that use their own timer).
2. Refactor `NisRecordingService`:
   - `WriteRecord(DataRecordType type, ushort instanceId, DateTime time, ISerializableRecord payload)` generic API.
   - Existing `IntegratedInsOutput_Data` write path becomes one caller.
   - Single lock around the FileStream, ordering preserved by enqueue.
   - Subscribes to `IntegratedOutputProducer.SnapshotProduced` when recording
     is on; writes type-50 records.
3. Clean up `IntegratedInsOutput_Data` per decision 5:
   - Drop lock, drop clone-on-accessor, drop stored `VelocityTotal`, fix
     `Clear()`.
   - Update `BinLength` constant (one less double).
   - Update `Encode` and `ReadBinary` (drop VelocityTotal write/read).
   - Update PARENT_PATCHES copy accordingly.
4. Delete `CsvTestingService.cs` and remove its DI registration.
5. Update `IntegrationSnapshotService` references in DI / callers to use
   `IntegratedOutputProducer`.

**Definition of done:**
- App launches, recording still produces a valid .dat file readable by the
  current g2-master RecordDecoderPro (before our PARENT_PATCHES land).
- `Recordings/TestLog_*.csv` no longer appears for new recordings.
- Hz can be configured (read from a config file or hardcoded default 100 Hz
  for now; UI hookup comes in Phase 3).
- Subscriber lifecycle observable: when recording stops and no sender is
  active, the producer's timer is disposed (verify via debugger).

**Don't do in this phase:** add new record types, add senders, touch UI,
touch `PlaybackInsDevice` or `CsvPlaybackService` (see decision 15 - they're
intentionally orphaned, not removed).

---

### Phase 2 - Per-INS raw recording + PARENT_PATCHES delivery

**Goal:** raw inbound packets from each connected device get written to the
.dat as `VN310_INS` / `Tmaps100_INS` records. RecordDecoderPro patches ready
for the user to apply.

**Steps:**
1. Author `Vn310InsRecord.cs` in `Recording/Payloads/`:
   - Wire-only struct (separate from in-memory `Vn310Telemetry`).
   - Fields: UtcTime, Lat/Lon/Alt, YPR, rates (+ HasRates flag), NED velocity,
     uncertainties (+ HasUncertainties flag), InsStatus raw word, TimeStatus
     raw byte.
   - `Serialize(BinaryWriter)` + static `Deserialize(BinaryReader)` +
     `IReadOnlyList<(string Column, string Value)> ToColumns()`.
2. Author `Vn310RawRecordWriter.cs` in `Recording/Writers/`:
   - Constructor takes `Vn310InsDevice`, `INisRecordingService`.
   - Subscribes to `Vn310InsDevice.TelemetryUpdated`.
   - On packet: maps in-memory `Vn310Telemetry` -> wire `Vn310InsRecord`,
     calls `recordingService.WriteRecord(VN310_INS, instanceId, packetReceivedAt, record)`.
3. Wire `Vn310RawRecordWriter` into DI. One per VN310 device instance (so
   `header.ID` is set correctly).
4. PARENT_PATCHES authoring:
   - Replace `VN310Item.cs`: drops VectorNav SDK dependency, reads
     `Vn310InsRecord.Deserialize(reader)`, calls `ToColumns()` to fill dict.
   - Add new `Vn310InsRecord.cs` to `PARENT_PATCHES/Infrastructure/Navigation/NavigationSystems/Records/`.
   - Replace `IntegratedInsOutputItem.cs`: reads cleaned-up `IntegratedInsOutput_Data`
     (drops VelocityTotal from read, derives at column-render time).
   - Replace `IntegratedInsOutput_Data.cs` + `IntegratedInsOutputStatusFlags.cs`
     + `IntegratedInsOutput_CommFrame.cs` (mirror NIS Recording/Payloads/ versions).
   - Patch `MainWindow.xaml.cs`:
     - Drop the `Enum.IsDefined` throw at line 142; replace with `continue`
       (log to Console for now; proper log routing is bug 6, defer).
     - The `VN310_INS` case still dispatches to `VN310Item` (which is now
       rewritten). No central-switch change.
5. Write `PARENT_PATCHES/_README.md` with the full file table:
   ```
   | NIS source path                        | Parent destination path                                  | Action  |
   |----------------------------------------|----------------------------------------------------------|---------|
   | RecordDecoderPro/ItemTemplates/VN310Item.cs | Utils/RecordDecoderPro/ItemTemplates/VN310Item.cs | REPLACE |
   | ...                                    | ...                                                      | ...     |
   ```
6. Defer TMAPS pieces (no hardware code yet); stub `Tmaps100xInsRecord` empty
   so the future Phase 2.5 has the template ready.

**Definition of done:**
- Recording session with VN310 connected produces a .dat with interleaved
  `VN310_INS` (per packet) and `IntegratedInsOutputRawData` (per tick) records.
- The user applies PARENT_PATCHES to their RecordDecoderPro copy and decodes
  the .dat: per-VN310-packet rows and per-tick integrated rows both appear in
  the output CSV.
- `header.ID` column shows the VN310's instance ID (0 for the only one).
- Two-VN310 scenario tested in principle (NIS doesn't support
  multi-instance-per-class yet; mark this as a future verification).

**Don't do in this phase:** anything outbound. No senders, no UDP.

---

### Phase 3 - EmulationMode senders + Settings UI

**Goal:** the user can pick `EmulationMode` and tick `Hz` in the Settings
page, and NIS transmits integrated data over UDP at the configured rate in
the configured persona.

**Steps:**
1. Author `EmulationMode.cs` enum, `UdpEndpoint.cs`, `EmulationSettings.cs`.
2. Author `IEmulationModeSender.cs` interface.
3. Author `IntegratedEmulationSender.cs`:
   - Owns its own `Timer` at `RateHz`.
   - Each tick: `producer.LatestSnapshot` -> serialize via
     `IntegratedInsOutput_CommFrame.Encode` -> send via `BaseUdpTransmitter`.
4. Author `Vn310EmulationSender.cs`:
   - Same shape as Integrated, but formats per VN310 ASCII VNINS protocol:
     `$VNINS,<time>,<week>,<status>,<yaw>,<pitch>,<roll>,<lat>,<lon>,<alt>,<velN>,<velE>,<velD>,<attU>,<posU>,<velU>*<CC>\r\n`.
   - Uses the integrated snapshot's values (NIS as a virtual VN310). Sets
     `attU/posU/velU = 0` (we don't track integrated uncertainties).
   - InstanceId reported in the message header is configurable in settings.
5. Author `EmulationModeService.cs`:
   - Holds `CurrentMode` + dispatch.
   - `SwitchTo(mode)`: `current?.Stop()`, instantiate sender for new mode,
     `current.Start()`. Atomic transition.
6. Author Settings page UI:
   - `SettingsPage.xaml` with sections (only Output/Emulation populated now).
   - VM: `SettingsPageViewModel` + `EmulationSettingsViewModel`.
   - Bindings per decision 9.
   - Hz textbox: validation logic (1..1000, 1000 mod N == 0). Invalid -> red
     border + error text + Save disabled.
   - Endpoint per-mode: stored in `EmulationSettings.EndpointsByMode`.
     Switching mode reloads endpoint into the textboxes.
7. Wire the Settings page into top-level nav.
8. Persist `EmulationSettings` to disk (alongside `DevicesConfig`). Load on
   startup. Apply via `EmulationModeService.SwitchTo` if `CurrentMode != None`.

**Definition of done:**
- Settings page renders; user can pick EmulationMode and Hz.
- With VN310 connected + integration grid showing real values + EmulationMode=Integrated:
  a tcpdump/Wireshark on the target endpoint shows binary frames arriving at
  the configured rate.
- Switch to EmulationMode=Vn310, point at a different endpoint: ASCII `$VNINS,...`
  lines arrive at 40 Hz (or whatever Hz was picked).
- Hz textbox rejects invalid input (e.g., 33, 60, 75) with a clear error.
- Restart app: settings persist, sender resumes if it was running.

**Don't do in this phase:** playback page, comparison graphs.

---

### Phase 4 - Playback page shell

**Goal:** standalone Playback top-level page with file open, transport, and
provenance grid. No graphs yet.

**Steps:**
1. Author `PlaybackFileReaderService.cs`:
   - `LoadAsync(string filePath) -> PlaybackFileContents`
   - `PlaybackFileContents` = `{ IReadOnlyList<IntegratedRecord> integratedFrames, IReadOnlyDictionary<DeviceKey, IReadOnlyList<RawRecord>> rawByDevice, DateTime startUtc, DateTime endUtc }`.
   - Parses the .dat once on load, dispatches by `DataType`:
     - `IntegratedInsOutputRawData` -> `IntegratedInsOutput_Data.ReadBinary` -> `IntegratedRecord`.
     - `VN310_INS` -> `Vn310InsRecord.Deserialize` -> `RawRecord<Vn310InsRecord>`.
     - `Tmaps100_INS` -> stub for future.
   - Sorts each list by record time.
2. Author `PlaybackPageViewModel.cs`:
   - `LoadedFile`, `CurrentTime`, `IsPlaying`, `PlaybackSpeed`.
   - `Play()`, `Pause()`, `Stop()`, `Seek(DateTime)`, `SetSpeed(double)`.
   - Internal `DispatcherTimer` (or stopwatch + frame request) drives
     `CurrentTime` while playing.
   - Computes `CurrentIntegratedFrame` via binary-search of
     `integratedFrames` by `CurrentTime`. Updates on every tick.
3. Author `PlaybackPage.xaml`:
   - File picker button at top + filename display.
   - UTC clock label bound to `CurrentIntegratedFrame.OutputTime`.
   - `TransportControl` (own user control): play/pause/stop buttons + seek
     slider (bound to `CurrentTime` against `startUtc..endUtc` range) + speed
     buttons.
   - Provenance grid: 14 rows (one per integration field), 3 columns (Device
     name, Instance ID, Value). Bound to `CurrentIntegratedFrame` via
     a converter that maps the field name to its triplet.
4. Author `TransportControl.xaml(.cs)`: reusable.
5. Wire into top-level nav.

**Definition of done:**
- User opens a Phase-2-recorded .dat. UTC clock + provenance grid populate
  immediately (showing frame 0).
- Play button advances time at real speed; provenance grid updates as the
  current frame changes.
- Seek slider works. Speed buttons work (0.5x, 1x, 2x, 5x).
- File with only integrated records (no raw) still works (no graph panel yet
  so raw records are ignored at this phase).

**Don't do in this phase:** comparison graphs.

---

### Phase 5 - Comparison graphs

**Goal:** the playback page's graphs panel shows one line per recorded
`(DeviceType, InstanceId)` per selectable field, with a synced vertical cursor.

**Steps:**
1. Add NuGet `ScottPlot.WinUI` to `NavigationIntegrationSystem.UI.csproj`.
   Run `dotnet restore`. Verify .nuget cache has `ScottPlot.WinUI`,
   `SkiaSharp`, `SkiaSharp.NativeAssets.Win32`.
2. Author `ComparisonGraph.xaml(.cs)`:
   - Wraps `WinUIPlot`.
   - DPs: `FieldName` (which integration field to plot), `RawByDevice`
     (the parsed raw streams from `PlaybackFileContents`), `CursorTime`
     (synced to playback `CurrentTime`).
   - On `RawByDevice` change: clears the plot, iterates each `(DeviceKey, List<RawRecord>)`
     pair, extracts the value of `FieldName` from each record (via a small
     field-extractor map), adds as a `ScottPlot.Plottables.Signal` or
     `Scatter` series.
   - Color per device key derived from a stable palette (`DeviceColorPalette.GetColor((type, id))`).
   - On `CursorTime` change: moves a vertical line annotation.
3. Add a "Field selector" UI element on the playback page: dropdown listing
   the 14 integration fields. Selection drives which `ComparisonGraph`
   instances are visible.
4. Layout: render N `ComparisonGraph` instances stacked vertically in a
   `ScrollViewer`, one per user-selected field. Default: all fields
   collapsed, user expands the ones they want to see.
5. Performance: ScottPlot's `Signal` plottable does internal decimation;
   should handle 360k points/series at interactive frame rates. Verify with
   a 1-hour test recording.

**Definition of done:**
- User opens a Phase-2 recording (VN310 connected during recording).
- Expands the "Yaw" graph. Sees one line per recorded VN310 instance with the
  raw yaw values over time.
- Pressing Play moves the cursor across all graphs in sync with the provenance
  grid.
- With two-INS recordings (when TMAPS hardware lands), each device's line is
  distinguishable by color.

**Don't do in this phase:** export, annotation, zoom-to-selection, multi-axis
panes. Those are nice-to-haves for later.

---

## MIGRATION_NOTES updates per phase

| Phase | MIGRATION_NOTES section touched |
|---|---|
| 0 | Section 2 (Yaw/Azimuth): rewrite from "asymmetry" framing to "migration plan: both sides moving to Yaw, coordinate at merge". |
| 1 | Section 5 (Behavioral changes): add producer/consumer recording pipeline. Section 4: add the moved-out-of-TO_BE_DELETED files. |
| 2 | New section: pointer to PARENT_PATCHES/_README.md. Section 5: raw-per-device recording is new behavior the parent doesn't have. |
| 3 | Section 5: EmulationMode service is new behavior. |
| 4 | Section 5: Playback page is new behavior (parent has no equivalent yet). |
| 5 | Section 3 (Library dependencies): add ScottPlot.WinUI + SkiaSharp. |

---

## PARENT_PATCHES/_README.md template

```markdown
# PARENT_PATCHES - NIS-authored files for the parent solution

Apply these on merge day or when synchronizing the parent's RecordDecoderPro
with NIS's record schema. Each row tells you where the file came from
(NIS) and where it goes (parent solution root).

## File table

| NIS source                                                        | Parent destination                                                          | Action  |
|-------------------------------------------------------------------|-----------------------------------------------------------------------------|---------|
| PARENT_PATCHES/RecordDecoderPro/ItemTemplates/VN310Item.cs        | Utils/RecordDecoderPro/ItemTemplates/VN310Item.cs                           | REPLACE |
| PARENT_PATCHES/RecordDecoderPro/ItemTemplates/TmapsItem.cs        | Utils/RecordDecoderPro/ItemTemplates/TmapsItem.cs                           | REPLACE |
| PARENT_PATCHES/RecordDecoderPro/ItemTemplates/IntegratedInsOutputItem.cs | Utils/RecordDecoderPro/ItemTemplates/IntegratedInsOutputItem.cs      | REPLACE |
| PARENT_PATCHES/RecordDecoderPro/MainWindow.xaml.cs.patch          | Utils/RecordDecoderPro/MainWindow.xaml.cs                                   | PATCH   |
| PARENT_PATCHES/Infrastructure/Navigation/NavigationSystems/IntegratedInsOutput/IntegratedInsOutput_Data.cs | Infrastructures/Infrastructure/Navigation/NavigationSystems/IntegratedInsOutput/IntegratedInsOutput_Data.cs | REPLACE |
| PARENT_PATCHES/Infrastructure/Navigation/NavigationSystems/IntegratedInsOutput/IntegratedInsOutputStatusFlags.cs | (same path) | REPLACE |
| PARENT_PATCHES/Infrastructure/Navigation/NavigationSystems/IntegratedInsOutput/IntegratedInsOutput_CommFrame.cs | (same path) | REPLACE |
| PARENT_PATCHES/Infrastructure/Navigation/NavigationSystems/Records/Vn310InsRecord.cs | Infrastructures/Infrastructure/Navigation/NavigationSystems/Records/Vn310InsRecord.cs | ADD |
| PARENT_PATCHES/Infrastructure/Navigation/NavigationSystems/Records/Tmaps100xInsRecord.cs | (same path) | ADD |

## Verification after applying

1. RecordDecoderPro builds in Visual Studio (parent solution).
2. Decoding a NIS-produced .dat (from Phase 2+) produces a CSV with all
   expected columns including the new `DeviceInstanceID` column populated
   from `header.ID`.
3. No "DataType N is not defined" exceptions on unknown record types
   (the throw was replaced with log+skip).
```

---

## Status tracker

Update inline as phases ship.

- [ ] Phase 0 - Yaw rename
- [ ] Phase 1 - Foundation refactor (producer/consumer + IntegratedInsOutput_Data cleanup + drop CSV)
- [ ] Phase 2 - Per-INS raw recording + PARENT_PATCHES delivery
- [ ] Phase 3 - EmulationMode senders + Settings UI
- [ ] Phase 4 - Playback page shell (file open, transport, provenance grid)
- [ ] Phase 5 - Comparison graphs

---

## References

- `CLAUDE.md` (project) - region order, MVVM rules, code style.
- `VN310_PLAN.md` - VN310 telemetry plan, Phase 7 partial.
- `MIGRATION_NOTES.md` - merge-day reconciliation.
- Memory:
  - `project_vn310_vnins_position_is_ins_derived.md` - VN310 quirk knowledge.
  - `feedback_logging_discipline.md` - lifecycle-only logging.
  - `feedback_per_device_pane_architecture.md` - per-device pane pattern (relevant for playback page if we add per-device panes later).
  - `feedback_to_be_deleted.md` - TO_BE_DELETED is untouchable (modified by this plan: see decision 13 for exception).
- g2-master files of interest:
  - `Utils/RecordDecoderPro/MainWindow.xaml.cs` - decode loop, central switch.
  - `Utils/RecordDecoderPro/ItemTemplates/VN310Item.cs` - to be replaced.
  - `Utils/RecordDecoderPro/ItemTemplates/TmapsItem.cs` - to be replaced.
  - `Infrastructures/Infrastructure/Enums/DataRecordID.cs` - DataRecordType enum.
  - `Infrastructures/Infrastructure.FileManagement/DataRecording/DataRecordHeader.cs` - 16-byte header confirmed.
  - `Infrastructures/Infrastructure.DataCommunication/Channels/BaseUdpTransmitter.cs` - UDP send primitive to vendor.

---

*Last updated by Claude on 2026-05-27, end of brainstorming session.*
