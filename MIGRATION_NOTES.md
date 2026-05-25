# Migration Notes — NIS → Parent Solution

Things to do when integrating `NavigationIntegrationSystem` into its eventual
parent solution. Living document; append to it whenever NIS makes a change
that would conflict with the parent's existing types or schemas.

**Important — what "parent solution" means here:** NIS is being built as a
standalone WinUI app, but will eventually be folded into a larger umbrella
solution as one of many projects. That umbrella solution is **not** any of the
external reference codebases I researched during planning (e.g. the
`OrbitNavSystemCtrl` family — `Navigation-LEAN/...` and `Navigation/...`
on disk). Those were studied to learn the VN310 protocol; they are *not*
the merge target and their conventions are not authoritative. The actual
parent's conventions will only become known at merge time.

The `TO_BE_DELETED/` folder *does* hold real artifacts from the actual parent
(per CLAUDE.md and project memory). Anything declared there reflects the
parent's real schema; everything else here is provisional until merge.

Order entries newest-first within each section so we don't lose track.

---

## 1. Files in `Infrastructure/TO_BE_DELETED/` — to remove on merge

These were vendored from the parent solution so NIS could compile in isolation.
On merge, delete the entire `TO_BE_DELETED/` folder; the parent's originals
become the canonical copies.

- `IntegratedInsOutput_Data.cs`
- `IntegratedInsOutputItem.cs`
- `IntegratedInsOutputStatusFlags.cs`
- (anything else that ends up in this folder)

After deletion, update the NIS references that point at these types so they
resolve against the parent's namespaces instead. Affected NIS files:

- `UI/Services/Recording/IntegrationSnapshotService.cs`
- `UI/Services/Recording/CsvTestingService.cs`
- (search for `using Infrastructure.Navigation.NavigationSystems.IntegratedInsOutput;`
  and the related `Infrastructure.DataStructures` / `Infrastructure.Navigation`
  imports — adjust to whatever the merged namespaces are)

---

## 2. Schema / naming asymmetries to reconcile

NIS internally renames some fields for UI clarity but keeps the parent's
on-the-wire names on shared binary / CSV schemas. When merging, the parent
solution can adopt NIS's naming OR NIS can keep aliasing — decide per item.

### Yaw vs Azimuth (Euler angle 3)
- **NIS UI / integration grid:** uses `Yaw` (constants `IntegrationFieldNames.Yaw`,
  `IntegrationFieldNames.YawRate`; row label "Yaw" / "Yaw Rate").
- **Parent solution recorder + record schema:** uses `Azimuth`
  (`AzimuthDeviceCode`, `AzimuthDeviceId`, `EulerAzimuthValue`,
  `IntegratedInsOutputStatusFlags.AzimuthValid`, etc.).
- **NIS keeps the asymmetry** via:
  - `IntegrationFieldKeyMap.cs` maps `IntegrationFieldNames.Yaw` →
    `"EulerAzimuthValue"`.
  - `IntegrationSnapshotService.cs` switch case `IntegrationFieldNames.Yaw`
    still writes to `io_Data.AzimuthDeviceCode` etc.
- **On merge — decide:**
  1. Rename parent's fields/flags to `Yaw*` (binary schema changes, CSV header
     changes, RecordDecoderPro changes — biggest blast radius), or
  2. Keep parent's `Azimuth*` names and have NIS continue aliasing (the current
     state), or
  3. Add a translation layer in the recorder so both names work.

### TMAPS100X — Azimuth/Yaw still TBD
- `Tmaps100XDeviceModule` currently defines `AzimuthDeg` / `AzimuthRateDegS`.
- Decision pending: confirm against TMAPS100X ICD whether the device emits
  "Azimuth" (true heading from north) or "Yaw" (body rotation).
- See inline `// TBD:` comment in `Tmaps100XDeviceModule.cs`.

### Grid row order (avionics convention)
- NIS reorders the integration grid Euler rows to `Yaw → Pitch → Roll`
  (and rates similarly). Parent solution's UI (if it has an equivalent) may
  use the old `Roll → Pitch → Azimuth` order.
- This is UI-only; no schema impact.

---

## 3. Library dependencies

### VectorNav SDK
- **NIS vendors `VectorNav.dll` v1.1.5** at `lib/VectorNav/VectorNav.dll`, wired
  via `<Reference Include="VectorNav"><HintPath>..\..\lib\VectorNav\VectorNav.dll</HintPath><Private>true</Private></Reference>`
  in `src/NavigationIntegrationSystem.Devices/NavigationIntegrationSystem.Devices.csproj`.
- **Why vendored, not NuGet:** the `VectorNav` package id does not exist on
  nuget.org (`NU1101` on restore). The parent solution consumed it from its
  local `packages/` folder via old-style `packages.config`, not from a public
  feed. Phase 1 confirmed the net472-only DLL loads cleanly on .NET 8 at
  runtime (Phase 1 smoke test, 2026-05-20).
- **Source of the vendored DLL:** copied from
  `C:\Users\BARBIC\Desktop\Work\NavigationControlSystem\Navigation\Navigation\NavigationSystemsController\packages\VectorNav.1.1.5\lib\net472\VectorNav.dll`
  (external research artifact — see VN310_PLAN.md glossary; NOT part of the
  parent solution).
- The actual parent solution's stance on VectorNav is unknown until merge.
  Possibilities to check at merge time:
  - Parent already vendors the same DLL → drop NIS's `lib/VectorNav/`, point
    the `HintPath` at the parent's copy.
  - Parent vendors a different version → reconcile (prefer the newer; verify
    NIS still compiles).
  - Parent adds VectorNav to a private NuGet feed → switch NIS to a
    `PackageReference`; drop `lib/VectorNav/`.
  - Parent doesn't use VectorNav at all → keep the vendored DLL; NIS owns it.

### `System.IO.Ports`
- Required separately on .NET 8 as a NuGet package (it's only in-box on
  .NET Framework). Parent (net472) already has it; nothing to remove from NIS
  on merge unless the merged project targets net472 throughout.

---

## 4. Files / patterns superseded by parent equivalents

Whenever NIS has built a thing locally that the parent already does better,
list the NIS file here so the merge can prefer the parent's version.

- (none yet — will accumulate as we go)

---

## 5. Behavioral changes NIS introduces that the parent doesn't have

Things NIS does that the parent's existing code doesn't — confirm the parent
team is OK with each before merging.

### Global unhandled-exception logging (2026-05-25, Phase 7)
- `App.xaml.cs` wires three handlers: `AppDomain.CurrentDomain.UnhandledException`,
  `Application.UnhandledException` (WinUI 3), `TaskScheduler.UnobservedTaskException`.
- Each handler writes the exception (incl. stack trace) to the daily log via
  `ILogService.Error` before the process is allowed to terminate.
- Handlers do NOT suppress termination (`e.Handled` left at default `false`) —
  logging only, no behavior change beyond visibility.
- **Motivation:** vendored VectorNav SDK throws on its own background thread
  when the VN310 loses power while connected (see section 7); without these
  handlers, the process dies with zero log evidence. The handlers are not
  VN-specific — they capture any unhandled exception across the app.
- **On merge:** if the parent already wires global handlers, the two
  implementations need to be reconciled (probably keep one set, prefer the
  parent's). If the parent has none, NIS's can stay as-is.

## 7. Vendor SDK defects (worked around, not fixed at source)

Things the vendored VectorNav SDK does that we can't fix without forking it.
Documented here so the parent-solution merge can decide whether to fork +
patch or accept the limitation.

### VectorNav SDK throws on serial-notifications thread when VN310 loses power
- **Where:** `VectorNavLib/src/Communication/SerialPort.cs:290` — inside
  `HandleSerialPortNotifications`, the SDK's own background thread (named
  `VN.SerialPort (COMx)`).
- **Trigger:** sensor loses power while the COM port stays present (e.g. USB-to-
  serial bridge stays alive because the FTDI/CP210x chip is powered from USB,
  but the VN310 itself is unpowered). Win32 `ReadFile` returns errors; the
  SDK's loop exits with `_continueHandlingSerialPortEvents` still true and
  throws `new Exception()` unconditionally.
- **Impact:** unhandled exception on a thread NIS doesn't own → .NET 8
  terminates the process. We cannot catch it from our code.
- **Current mitigation:** none functional — the global exception handler logs
  the throw before death (see section 5) so we have post-mortem evidence.
  AutoReconnect cannot help because the process is already gone.
- **True fix (deferred):** rebuild the SDK from the source at
  `Navigation-LEAN/VectorNav/VN310/V5.0/VectorNavLib/`, replace the throw with
  an event raise (`ConnectionLost`), subscribe in `Vn310TelemetryService` and
  hook to existing `Stalled` + AutoReconnect machinery. Decision deferred to
  senior-engineer review — owning a forked SDK is a meaningful dependency
  change.
- **On merge:** if the parent solution patches or replaces this SDK, drop the
  workaround note from this file.

---

## 6. Cleanup checklist on merge day

Before opening the merge PR:

- [ ] Delete `Infrastructure/TO_BE_DELETED/` folder
- [ ] Resolve every Yaw/Azimuth asymmetry decision (section 2)
- [ ] Verify `VectorNav` package version matches parent
- [ ] Remove NIS-local `lib/` if used as NuGet fallback
- [ ] Re-run NIS + parent test suites together
- [ ] Search for any remaining `TO_BE_DELETED` references in NIS code
- [ ] Search for any `// TBD:` comments in NIS code and resolve

---

*Last updated by Claude during VN310-planning session.*
