# NIS Cleanup Phases

Tracking remaining work from the 2026-05-18 audit. Each phase is a self-contained commit that can be reviewed independently.

---

## Phase 1 — Correctness bugs in our code ✅ DONE

Shipped: data-correctness fixes outside `TO_BE_DELETED`. Build clean, no errors.

- [x] `PlaybackInsDevice.OnConnectAsync` — replaced `.Wait()` with `await ... ConfigureAwait(false)`; method is genuinely async
- [x] `InsDeviceRegistry.GetInstanceId` — throws `InvalidOperationException` on miss instead of silently returning 0
- [x] `IInsDeviceInstanceProvider` — interface doc updated
- [x] `InsDeviceBase` — `m_CtsLock` added; status guard + CTS mutation are now atomic; awaited tasks capture the token locally
- [x] `CsvPlaybackSchema.Columns` + `PlaybackDeviceModule` — realigned: schema now includes `RcvTime[sec]` and `OutputTime[sec]` at the head, matching the 17 device fields
- [x] `CsvPlaybackService.CreateTemplateAsync` — emits header + one zero-row so the template passes its own validator
- [x] `IntegrationSnapshotService.StartAsync` — captures the UI `DispatcherQueue` (no more NRE if recording-state callback fires on a non-UI thread)
- [x] `IntegrationSnapshotService.TakeSnapshot` — `OutputTime` set once per snapshot with a comment; removed misleading Latitude-coupling; dead `total` local removed
- [x] `MainWindow.xaml` + `NavKeys` + `NavigationService` + new `HelpPage.xaml` stub — Help menu item now navigates to a real (empty) page
- [x] `LogsPage.xaml` — `SelectAllButtonText` binding gets `Mode=OneWay` so the button label updates

---

## Phase 2 — Performance landmines ✅ DONE

Shipped: 100Hz capture is fully off the UI thread, disk-fsync hot paths eliminated, playback file streamed lazily. Build clean, no new warnings.

- [x] **Move 100Hz snapshot off the UI thread.** `IntegrationSnapshotService` now drives a background `PeriodicTimer`. Row VMs expose `CaptureSnapshotForRecording()` — a volatile-read of the selected source + a `Volatile.Read` of the candidate's value. UI thread no longer does any per-tick recording work.
- [x] **`CsvTestingService` write strategy.** Replaced `WriteThrough` + `AutoFlush` + per-row `Flush`+`BaseStream.Flush` with a buffered `StreamWriter` (64KB). Flush deferred until `Stop`. Locked for thread-safety since `PrintSnapshot` now runs on the background snapshot thread.
- [x] **`NisRecordingService` per-record flush removed.** A background `PeriodicTimer` flushes every 1s; final flush still happens on `Stop`. The legacy `Thread.Sleep(50)` is retained for Phase 4 with a comment pointing at the fire-and-forget `WriteAsync` root cause.
- [x] **`CsvPlaybackService.LoadFileAsync` lazy file indexing.** Replaced `File.ReadAllLinesAsync` with a one-pass byte-offset scan into `List<long>`. The `FileStream` stays open; data lines are read on demand at their indexed offsets. Memory: ~8 bytes per line + offsets only.
- [x] **`CsvPlaybackService.PlaybackLoopAsync` Stopwatch-based timing.** Frame deadlines tracked in `Stopwatch.Frequency` ticks so 60Hz (16.67ms) no longer drifts via integer truncation. Mid-playback frequency changes are honored next iteration. Falls back to "reset baseline" if more than ~100ms behind.
- [x] **`IntegratedInsOutput_Data` clone-on-accessor mitigation.** `IntegrationSnapshotService.TakeSnapshot` now reads `Position` / `EulerData` / `VelocityVector` exactly ONCE per snapshot, mutates the local clones across all rows of that group, then writes back exactly ONCE. Drops cloning cost from ~24 → 6 per snapshot.

---

## Phase 3 — Convention and architecture cleanup

Pattern violations and inconsistencies that don't break anything but make the codebase harder to maintain. Most of these are documented in `CLAUDE.md` and `Preferences.md` and currently violated.

- [ ] **Unify MVVM base.** `MainViewModel`, `DeviceCardViewModel`, `InspectFieldViewModel`, `DevicesViewModel`, `LogsViewModel` extend `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`. The project rule is manual `ViewModelBase` only. Migrate.
- [ ] **Drop dead `partial` modifiers.** No source generators are in use; `partial` on every VM is misleading. Remove.
- [ ] **Replace Service Locator in Pages.** Every Page does `((App)Application.Current).Services.GetRequiredService<TVm>()`. Use a DI-driven factory or page activation.
- [ ] **Magic strings.** `IntegrationSnapshotService` and `IntegrationViewModel` match field names by literal string (`"Latitude"`, `"Velocity North"`, etc.). Define `IntegrationFieldNames` constants in Core.
- [ ] **Delete duplicate converters.** `PaneModeToInspectVisibilityConverter` and `PaneModeToSettingsVisibilityConverter` are hardcoded clones of the already-parameterized `DevicesPaneModeToVisibilityConverter`.
- [ ] **Normalize `ConvertBack`.** Some throw `NotSupportedException`, some throw `NotImplementedException`, some return the input unchanged, some return `false`. Pick one (WinUI convention is `NotSupportedException`).
- [ ] **Fix `ILogPaths` cross-cast.** `HostBuilderFactory.cs:53` casts the `ILogService` singleton to `ILogPaths` — brittle. Register `FileLogService` as the concrete and bind both interfaces to that instance.
- [ ] **PascalCase the legacy-style props** in `Infrastructure/Recording/RecordTypeItem.cs`, `RecordFieldItem.cs`, `RecordIDItem.cs` (`recordTypeName` → `RecordTypeName`, etc.) — these are production code, not `TO_BE_DELETED`.
- [ ] **Mixed binding strategy.** `PlaybackSettingsView.xaml` and `RealDeviceSettingsView.xaml` use `{Binding}`, every other page uses `{x:Bind}`. Standardize on `x:Bind` where possible (compile-time-checked).
- [ ] **Playback as integration source actually sources nothing.** `NumericSourceCandidateViewModel` generates random walk data via `Tick()`, even for Playback-connected devices. When Playback is connected, the candidate should pull from `PlaybackInsDevice.Telemetry` keyed on the row's field key.
- [ ] **Round-trip `OutputTime` when Playback is the source.** Currently `OutputTime` is always set to snapshot-time in `TakeSnapshot`. When the Playback device is selected for any field, the source's `OutputTime[sec]` should drive `IntegratedInsOutput_Data.OutputTime` so a record-then-playback-then-record cycle preserves the original time.

---

## Phase 4 — Hacks rooted in TO_BE_DELETED

These can't be fixed at the source because the offending code lives in `TO_BE_DELETED` (which is staying until merge into the parent solution). The strategy is to wrap and document so our code doesn't pretend the underlying behavior is sane.

- [ ] **Wrap `BinaryFileRecorderEnhanced` behind a proper async interface in our code.** `BinaryFileRecorderEnhanced.Record` issues a fire-and-forget `WriteAsync` (root cause of the `Thread.Sleep(50)` in `NisRecordingService.Stop`). Add an internal queue + drain-on-stop in our wrapper service so the hack is contained and labeled.
- [ ] **Convert `FileLogService.WriteToFileAsync` to a background-queue consumer.** Currently `_ = WriteToFileAsync(record)` silently swallows disk errors. Use a `Channel<LogRecord>` with a single consumer; surface write failures to a dead-letter event.
- [ ] **`NisRecordingService.Start` writes a dummy `(99, 99)` filler record** to avoid 0-byte file deletion. Move this hack behind the wrapper interface from item 1, with a comment that points at the underlying recorder bug. Once `TO_BE_DELETED` is replaced with the real shared recorder, both hacks vanish.

---

## Open questions / future

- **Help page content** — currently a stub. Decide what goes here (keyboard shortcuts? troubleshooting? device-format reference?).
- **Speed parameter** — referenced in PROJECT_STATE.md but no row in the integration grid. Either remove from docs or add as a derived field.
- **Time format on the wire** — `RcvTime[sec]` and `OutputTime[sec]` are seconds-of-day (matches RecordDecoderPro). This wraps at midnight. For multi-day recordings or recordings crossing midnight, this is lossy. Revisit when round-trip semantics get implemented.

---

## How to resume

Each phase is intended to be tackled in a single session. Start by re-reading `CLAUDE.md` and this file, pick a phase, then attack the items top-down. Phase 2 is the highest-impact remaining work; Phase 3 is mostly cleanup; Phase 4 is contained-hack-management. Phases are independent — pick whichever fits the time available.
