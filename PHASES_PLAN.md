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

## Phase 3 — Convention and architecture cleanup ✅ DONE

Shipped: pattern violations cleaned up; Playback feature now produces real recordings instead of random-walk noise. Build clean, no new warnings.

- [x] **Unify MVVM base.** `MainViewModel`, `DeviceCardViewModel`, `InspectFieldViewModel`, `DevicesViewModel`, `LogsViewModel` migrated from `ObservableObject` to project `ViewModelBase`. Manual `SetProperty`/`OnPropertyChanged` pattern preserved.
- [ ] **Drop dead `partial` modifiers** — REVERTED. The premise was wrong: CsWinRT *is* a source generator. It emits partial extensions for VMs that cross the WinRT ABI (DataContext / x:Bind). Missing `partial` works on JIT today but breaks under trimming / AOT and triggers a WinRT info diagnostic. `partial` restored on all VMs + `ViewModelBase` + the new `PlaybackSourceCandidateViewModel`; CLAUDE.md updated.
- [x] **Replace Service Locator in Pages.** Introduced `App.GetService<T>()` static helper; 5 callers (`IntegrationPage`, `SettingsPage`, `DevicesPage`, `LogsPage`, `PlaybackTrayView`) now use the helper. Single resolution point; still service-locator at heart, but contained.
- [x] **Magic strings.** New `IntegrationFieldNames` constants in Core (`Latitude`, `Roll`, `VelocityNorth`, etc.). Wired through `IntegrationViewModel.InitializeIntegrationRows`, `UpdateCalculatedRow`, `CreateInitialValue`; `IntegrationFieldRowViewModel.IsCalculated`; `IntegrationSnapshotService.ApplyRowSnapshot`. Removed dead "Elevation"/"Speed" cases that referenced non-existent rows.
- [x] **Delete duplicate converters.** `PaneModeToInspectVisibilityConverter` and `PaneModeToSettingsVisibilityConverter` `.cs` files removed. XAML already used `DevicesPaneModeToVisibilityConverter` with `TargetMode` via `x:Key` aliases in `App.xaml` — the deleted classes were never referenced.
- [x] **Normalize `ConvertBack`.** All non-implementations now `throw new NotSupportedException()` (WinUI convention). 6 converters fixed; real implementations (`BoolToVisibilityConverter`, `SelectionToIntConverter`) untouched.
- [x] **Fix `ILogPaths` cross-cast.** `FileLogService` registered as concrete; `ILogService` and `ILogPaths` both bound to the same instance via `sp.GetRequiredService<FileLogService>()`. No more interface cross-cast.
- [ ] **PascalCase legacy-style props** — DEFERRED. Renaming would force a touch in `TO_BE_DELETED/RecordTypeItem.cs` (which references `recordFieldName`); per policy, TO_BE_DELETED stays untouched until parent-solution integration replaces it.
- [x] **Mixed binding strategy.** `PlaybackSettingsView` and `RealDeviceSettingsView` converted to `{x:Bind ViewModel.X}`. Code-behind subscribes to `DataContextChanged` and calls `Bindings.Update()` — matches the established pattern in `DeviceSettingsPaneView`.
- [x] **Playback as integration source.** New `PlaybackSourceCandidateViewModel : IntegrationSourceCandidateViewModel, IDisposable` subscribes to `IPlaybackService.PacketDispatched`, stores latest value via `Volatile.Write` (no cross-thread PropertyChanged), exposes `GetSnapshotValue()` for the 100Hz recording loop. UI display refreshes at 4Hz via the existing `Tick` cadence. `IntegrationViewModel.RebuildRowSources` branches on `DeviceType.Playback` using new `IntegrationFieldKeyMap` in Core. Disposable candidates are disposed before `Sources.Clear()` so playback unsubscribes cleanly on device-list rebuild.
- [ ] **Round-trip `OutputTime`** — DECIDED NOT TO DO. `OutputTime` continues to be `DateTime.UtcNow` regardless of source. Round-tripping via `OutputTime[sec]` is lossy across midnight (seconds-of-day only), and mixed-source recordings make "whose time wins" ambiguous. See spec: `docs/superpowers/specs/2026-05-19-phase-3-playback-as-source-design.md`.

---

## Phase 4 — Hacks rooted in TO_BE_DELETED ✅ DONE

Originally framed as "wrap TO_BE_DELETED quirks behind our own abstractions." On review the wrapping itself was speculative — the legacy `BinaryFileRecorderEnhanced` will be replaced by the parent solution's version at integration, and we don't yet know the replacement's API surface. We already have a stable abstraction in our code (`IRecordingService`); a second wrapper layer would harden against an API we don't control. So Phase 4 collapsed: real fix for the one item that's actually in our code, sharper comments on the two that depend on the legacy recorder.

- [x] **`FileLogService` background-queue consumer.** Replaced fire-and-forget `_ = WriteToFileAsync(record)` with a bounded `Channel<LogRecord>` (capacity 10k, Wait mode + non-blocking `TryWrite`, single reader). Producer never blocks; drops under sustained backpressure are counted via `DroppedRecordCount`. Disk-write failures surface via a new `WriteFailed` event instead of silent swallow. `Dispose` drains pending records with a 2s timeout so shutdown can't hang on a stuck disk.
- [ ] **Wrap `BinaryFileRecorderEnhanced`** — DECIDED NOT TO DO. The legacy recorder is in `TO_BE_DELETED` and gets replaced at parent-solution integration. Wrapping today speculatively hardens against an API we don't control. `IRecordingService` already gives us the stable boundary; a second wrapper adds layering without an interface gain. Comment in `NisRecordingService.Stop` now clearly labels the `Thread.Sleep(50)` as a placeholder paired with the legacy recorder's fire-and-forget `WriteAsync`, to be deleted once the replacement exposes a real completion contract.
- [ ] **Move the `(99, 99)` filler record behind the wrapper** — DECIDED NOT TO DO. Same reason as above. Comment in `NisRecordingService.Start` now explains the filler-record pattern paired 1-to-1 with the legacy `CloseCurrentFile` 0-byte deletion behavior. Removed at integration when the replacement recorder uses a different file lifecycle.

---

## Open questions / future

- **Help page content** — currently a stub. Decide what goes here (keyboard shortcuts? troubleshooting? device-format reference?).
- **Speed parameter** — referenced in PROJECT_STATE.md but no row in the integration grid. Either remove from docs or add as a derived field.

---

## How to resume

Each phase is intended to be tackled in a single session. Start by re-reading `CLAUDE.md` and this file, pick a phase, then attack the items top-down. Phase 2 is the highest-impact remaining work; Phase 3 is mostly cleanup; Phase 4 is contained-hack-management. Phases are independent — pick whichever fits the time available.
