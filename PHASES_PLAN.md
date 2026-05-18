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

## Phase 2 — Performance landmines

The app captures snapshots at 100Hz on the UI thread, hammers the disk with forced flushes, and loads playback files entirely into memory. Each of these is a real-world stall waiting to happen.

- [ ] **Move 100Hz snapshot off the UI thread.** `IntegrationSnapshotService` currently uses a `DispatcherQueueTimer` on the UI dispatcher. Iterate the row VMs by reading immutable snapshots; the actual record write should run on a background task. Either that or drop the rate to ~50Hz event-driven.
- [ ] **`CsvTestingService` write strategy.** Currently `FileOptions.WriteThrough` + `AutoFlush=true` + manual `Flush()` + `BaseStream.Flush()` per row = 4 fsyncs × 100/sec = 400 fsyncs/sec for a *testing* service. Use a single buffered writer, flush only on stop.
- [ ] **`NisRecordingService.RecordIntegratedOutput` flushes every record at 50Hz.** Move flush to a periodic task (or to Stop only) — 50 fsyncs/sec to the binary file is unnecessary.
- [ ] **`CsvPlaybackService.LoadFileAsync` uses `File.ReadAllLinesAsync`** — loads the whole file in memory. PROJECT_STATE.md explicitly required line-by-line `StreamReader`. Replace with streaming reader; index lines lazily.
- [ ] **`CsvPlaybackService.PlaybackLoopAsync` uses `1000 / Frequency` integer division.** At 60Hz that's 16ms not 16.67ms. Switch to `Stopwatch`-based elapsed-time accounting so jitter doesn't accumulate.
- [ ] **`IntegratedInsOutput_Data` clone-on-getter / clone-on-setter** is in `TO_BE_DELETED` so we can't fix it directly. Mitigate from the consumer side: stop reading/writing `Position`, `EulerData`, `VelocityVector` through their cloning accessors in the 100Hz hot path. Use direct field-by-field copies once per snapshot.

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
