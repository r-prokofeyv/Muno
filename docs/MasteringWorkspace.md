# Muno Mastering Workspace

## 1. Purpose

This document defines the user-facing mastering workflow in Muno: UI layout, controls, state transitions, track variants, user interactions, playback switching, waveform behavior, and reaction to PhaseLimiter progress and errors.

Technical details of how `phase_limiter.exe` is launched and how its CLI behaves are defined separately in `docs/PhaseLimiter.md`.

The two specifications are complementary:

- `PhaseLimiter.md` defines how Muno communicates with the mastering engine.
- `MasteringWorkspace.md` defines how the Muno feature behaves from the application's and user's point of view.

## 2. Existing application context

The existing WinUI 3 application already supports:

- opening an audio file;
- rendering its waveform;
- playback and pause;
- seeking;
- displaying current playback position and duration.

The current screen has a large waveform area, transport controls above it, and the existing Open File command in the top command area.

The mastering feature must extend this screen and reuse the existing audio playback and waveform implementations. It must not introduce a second independent playback subsystem or a separate waveform implementation.

## 3. Core concept

A loaded file becomes the `Original track` for the current mastering session.

Every mastering run creates a new independent variant from the same Original track:

- Original track
- Mastering 1
- Mastering 2
- Mastering 3
- ...

A new mastering run must always use the Original track as PhaseLimiter input, regardless of which variant is currently selected or playing.

Example:

- user loads `song.wav`;
- creates Mastering 1;
- listens to Mastering 1;
- changes settings and starts mastering again;
- Mastering 2 is created from `song.wav`, not from Mastering 1.

This prevents cumulative mastering and makes all generated variants directly comparable with the same source.

## 4. Screen layout

The existing screen should be reorganized into two main columns below the existing top command area.

### 4.1 Main content column

The main content column occupies all remaining width except the right variants panel.

Its vertical order is:

1. existing transport/playback area;
2. Mastering Settings panel;
3. existing waveform, filling the remaining available space.

The waveform remains the dominant visual element of the application.

### 4.2 Right variants panel

Add a fixed-width right-side panel for track variants.

Recommended desktop width:

- approximately 280-320 px;
- should not grow together with the waveform area;
- separated visually from the main content by spacing or a subtle separator.

The panel contains:

1. header `Versions`;
2. a vertically scrolling list of track variants.

The first item is always `Original track` after a source file has been loaded.

Generated items are appended below it in creation order:

- Mastering 1
- Mastering 2
- Mastering 3
- ...

## 5. Mastering Settings panel layout

Place the Mastering Settings panel in the main content column between the existing transport area and the waveform.

The panel has two logical rows.

### Row 1: main mastering controls

Place from left to right:

1. Target Loudness;
2. Mastering Intensity;
3. True Peak Ceiling;
4. Preserve Bass;
5. Master button.

The three range controls use equal visual sizing and should read as one group.

`Master` is the primary action of this panel and should be visually distinct from ordinary settings controls. Place it at the right side of the first row.

### Row 2: algorithm and output controls

Place from left to right:

1. Optimization Algorithm;
2. Output Format;
3. Bit Depth;
4. Sample Rate.

`Bit Depth` is visible only when Output Format is WAV. When it is hidden, the remaining controls keep their logical order and the layout should not leave a large unusable gap.

The settings panel should remain compact and must not push the waveform into a secondary role.

## 6. User-facing controls and parameter mapping

Full engine semantics and fixed PhaseLimiter parameters are defined in `docs/PhaseLimiter.md`. This section defines the UI-to-setting mapping required by the feature.

| UI control | WinUI control type | UI values | Muno default | MasteringSettings value | PhaseLimiter CLI |
|---|---|---|---|---|---|
| Target Loudness | `CommunityToolkit.WinUI.Controls.RadialGauge` | -20 ... 0 LUFS, step 1 | -9 | `TargetLoudness` | `--reference=<value>` with fixed `--reference_mode=loudness` |
| Mastering Intensity | `CommunityToolkit.WinUI.Controls.RadialGauge` | 0 ... 100% | 100% | `MasteringIntensity` as 0.0 ... 1.0 | `--mastering5_mastering_level=<value>` |
| True Peak Ceiling | `CommunityToolkit.WinUI.Controls.RadialGauge` | -6 ... 0 dBTP, step 0.1 | 0 | `TruePeakCeiling` | `--ceiling=<value>` |
| Preserve Bass | `ToggleSwitch` | Off / On | Off | `PreserveBass` | `--erb_eval_func_weighting=<bool>` |
| Optimization Algorithm | `CommunityToolkit.WinUI.Controls.Segmented` | DE / NM / PSO / DE-PRMM / PSO-DV | DE-PRMM | `OptimizationAlgorithm` | `--mastering5_optimization_algorithm=<value>` |
| Output Format | `CommunityToolkit.WinUI.Controls.Segmented` | WAV / MP3 / AAC | WAV | `OutputFormat` | `--output_format=<value>` |
| Bit Depth | `CommunityToolkit.WinUI.Controls.Segmented` | 16-bit / 24-bit / 32-bit Float | 16-bit | `BitDepth` | `--bit_depth=<value>`; relevant only for WAV |
| Sample Rate | `CommunityToolkit.WinUI.Controls.Segmented` | 44.1 / 48 / 88.2 / 96 kHz | 44.1 kHz | `SampleRate` | `--sample_rate=<value>` |

Optimization Algorithm mapping:

- DE -> `de`
- NM -> `nm`
- PSO -> `pso`
- DE-PRMM -> `de_prmm`
- PSO-DV -> `pso_dv`

Output Format mapping:

- WAV -> `wav`
- MP3 -> `mp3`
- AAC -> `aac`

Bit Depth mapping:

- 16-bit -> `16`
- 24-bit -> `24`
- 32-bit Float -> `32`

Sample Rate mapping:

- 44.1 kHz -> `44100`
- 48 kHz -> `48000`
- 88.2 kHz -> `88200`
- 96 kHz -> `96000`

All user-facing settings that affect the generated file, including output settings, are part of the settings snapshot used to determine whether a new mastering can be created.

## 7. Track variant model

A track variant represents one playable item in the right panel.

Each variant must contain at least:

- `DisplayName`;
- `AudioFilePath`;
- variant kind: Original or Mastering;
- `State`;
- `Progress`;
- the immutable `MasteringSettings` snapshot used to create it, for mastering variants;
- diagnostics required for a failed mastering result.

Variant states:

- `Ready`;
- `Processing`;
- `Failed`.

`Original track` is always Ready and has no mastering settings snapshot.

## 8. Track variant visual behavior

### Ready

A Ready item:

- can be selected when no mastering process is running;
- can be used for playback;
- does not display a progress bar.

The currently selected item must be visually distinguishable from the other items.

### Processing

A Processing item:

- cannot be selected;
- displays a progress bar;
- may display the numeric percentage if it fits the current visual style;
- remains in the list from the moment the job starts.

### Failed

A Failed item:

- remains visible in the list;
- cannot be selected for playback;
- no longer displays a progress bar;
- visually indicates `Failed`;
- retains diagnostic information for logging/error handling.

A failed item keeps its number. If Mastering 2 fails, the next run creates Mastering 3.

## 9. Shared playback position

Original and all Ready mastering variants participate in one A/B comparison timeline.

The current playback position is treated as a shared logical position for the session.

When switching from one Ready variant to another:

1. capture the current playback position;
2. stop/suspend the current source as required by the existing playback implementation;
3. load the selected variant as the playback source;
4. seek to the captured position;
5. immediately start playback.

Example:

- Original is playing at 00:42;
- user selects Mastering 1;
- Original stops;
- Mastering 1 starts at 00:42.

Later:

- Mastering 1 reaches 01:30;
- user selects Mastering 2;
- Mastering 1 stops;
- Mastering 2 starts at 01:30.

If the captured position is beyond the selected file duration, clamp it to the selected file duration.

Selecting another Ready variant is an explicit A/B action and starts playback immediately even if the previously selected variant was paused.

## 10. Waveform behavior

The waveform always represents the currently selected playable variant.

When the selected variant changes:

- reuse the existing waveform loading/generation mechanism;
- update the waveform for the newly selected file;
- preserve the shared playback position;
- keep the playhead synchronized with that position.

Playback source switching should not unnecessarily wait for waveform generation. If waveform calculation is asynchronous, playback may start first and waveform can update when ready, provided the playhead remains synchronized.

## 11. Master button state

The Master button is enabled when all of the following are true:

- an Original track is loaded;
- no mastering process is currently running;
- either no successful mastering has been created yet, or current `MasteringSettings` differ from the settings snapshot of the last successful mastering run.

The comparison includes all user-facing mastering and output settings.

After Master is invoked:

- the button becomes disabled immediately;
- it remains disabled for the whole active mastering job.

After a successful job:

- if current settings still equal the snapshot used by that job, Master remains disabled;
- if the user changed any setting while the job was running, Master becomes enabled after the job finishes;
- if the user changes settings and then restores every value to the last successful snapshot, Master becomes disabled again.

After a failed job:

- Master becomes enabled again so the user can retry with the same settings.

This behavior should be derived from settings equality and processing state, rather than from an isolated mutable `IsDirty` flag.

## 12. Mastering job behavior

When Master is invoked:

1. capture an immutable snapshot of current MasteringSettings;
2. allocate the next mastering number;
3. create `Mastering N` immediately;
4. set its state to Processing and progress to 0;
5. append it to the right variants list;
6. create a unique output file path appropriate for the selected Output Format;
7. start PhaseLimiter using the Original track as input and the captured settings snapshot;
8. update the Processing item's progress from PhaseLimiter progress events.

Only one mastering job can run at a time.

The active job must never read live values from UI controls after it has started. It operates exclusively on its settings snapshot.

## 13. Interaction while mastering is running

While a mastering job is active:

- Master is disabled;
- the new Processing item cannot be selected;
- all existing Mastering items are temporarily unavailable for new selection;
- Original track remains selectable;
- the source that was already playing when mastering started may continue playing;
- playback, pause and seek continue to work for the current source;
- mastering settings remain editable so the user can prepare the next variant;
- edits do not affect the active job.

If the currently selected source is an existing Mastering item when a new job begins, it may continue playing. Disabling mastering items during processing prevents changing to another mastering item; it does not forcibly stop the already playing source.

The Open File command should be disabled while a mastering job is active so the current session cannot be replaced underneath the running job.

## 14. Successful job completion

A mastering job is successful only when the PhaseLimiter integration reports success according to `docs/PhaseLimiter.md`.

On success:

1. set the item's progress to 100%;
2. set its state to Ready;
3. capture the current shared playback position at the moment the job completes;
4. make the new Mastering N the selected variant;
5. switch playback to the generated output file;
6. seek to the captured position;
7. immediately start playback;
8. load/update waveform for the new file;
9. re-enable selection of Ready mastering variants;
10. recalculate Master button availability from current settings versus the completed job snapshot.

The position used after completion is the current position at completion time, not the position from when Master was originally pressed.

Example:

- mastering starts while Original is at 00:15;
- user continues listening and seeks to 00:52;
- mastering completes;
- new Mastering N starts at 00:52.

## 15. Failed job completion

If mastering fails:

- set the item's state to Failed;
- stop showing its progress bar;
- keep the item in the list;
- keep existing Original/Ready files intact;
- retain error diagnostics;
- do not automatically change the selected playback source;
- re-enable existing Ready variants;
- re-enable Master so the user can retry with the same settings.

The application must remain usable after a PhaseLimiter failure or native process crash.

## 16. Loading a new Original track

When no mastering job is active and the user opens another audio file:

1. stop the current playback;
2. replace the current Original track with the newly opened file;
3. clear all previous Mastering variants from the UI session;
4. reset mastering numbering so the next result is Mastering 1;
5. clear the last successful mastering settings snapshot;
6. select the new Original track;
7. load its waveform using the existing mechanism;
8. use the existing default/start playback behavior of the application;
9. recalculate Master availability for the new session.

Temporary files from the previous session may be cleaned up by the implementation as appropriate, without blocking the UI.

## 17. Closing the application during mastering

If the application closes while a mastering job is active:

- cancel/terminate the active PhaseLimiter process through the engine contract;
- terminate its process tree so child FFmpeg processes are not left running;
- do not block application shutdown indefinitely waiting for the mastering process.

## 18. Output-dependent UI behavior

### WAV

When Output Format is WAV:

- Bit Depth is visible and enabled;
- selected Bit Depth participates in settings snapshot equality;
- output extension is `.wav`.

### MP3

When Output Format is MP3:

- Bit Depth is collapsed;
- the stale Bit Depth value may remain in MasteringSettings but does not participate in the effective PhaseLimiter request for this format;
- output extension is `.mp3`.

### AAC

When Output Format is AAC:

- Bit Depth is collapsed;
- the stale Bit Depth value may remain in MasteringSettings but does not participate in the effective PhaseLimiter request for this format;
- output extension is `.aac`.

Changing Output Format or Sample Rate counts as a settings change and may enable Master.

## 19. User scenarios

### US-01: Application starts without a track

Expected behavior:

- no Original track exists;
- variants list is empty;
- Master is disabled;
- mastering controls are visible but should not allow starting a job;
- existing Open File workflow remains available.

### US-02: User opens an audio file

Expected behavior:

- file becomes Original track;
- Original track is selected;
- waveform is loaded through the existing implementation;
- playback uses the existing implementation;
- Master becomes available because there is no successful mastering snapshot yet.

### US-03: User changes mastering settings

Expected behavior:

- MasteringSettings updates through binding;
- no audio file is modified;
- if current settings differ from the last successful snapshot and no job is running, Master becomes enabled;
- restoring all values to the last successful snapshot disables Master again.

### US-04: User starts mastering while listening to Original

Expected behavior:

- snapshot current settings;
- append Mastering N in Processing state;
- Master disables;
- Original may continue playing and may be seeked/paused;
- progress updates in Mastering N;
- PhaseLimiter input is Original track.

### US-05: User starts mastering while listening to an existing Mastering variant

Expected behavior:

- PhaseLimiter still uses Original track as input;
- current selected mastering variant may continue playing;
- Mastering N is appended as Processing;
- existing mastering items cannot be newly selected until the active job finishes.

### US-06: User changes settings while mastering is running

Expected behavior:

- active job continues with its immutable snapshot;
- UI controls show the new values;
- Master stays disabled until active job ends;
- after successful completion, Master becomes enabled if current settings differ from the completed snapshot.

### US-07: Mastering succeeds

Expected behavior:

- Processing item becomes Ready;
- progress becomes 100%;
- current playback position is captured at completion time;
- new variant becomes selected;
- playback switches to the new file at the captured position and starts immediately;
- waveform switches to the new file;
- Ready mastering items become selectable again.

### US-08: User A/B switches between Ready variants

Expected behavior:

- capture current position;
- switch source;
- seek new source to the same position;
- begin playback immediately;
- update waveform and playhead for the selected variant.

### US-09: User creates several variants

Expected behavior:

- variants are appended sequentially;
- every run uses Original track as input;
- every variant preserves its own settings snapshot and output file;
- selecting a variant does not change the settings used to create another variant unless the user explicitly changes the controls.

### US-10: Mastering fails

Expected behavior:

- item becomes Failed and remains visible;
- selected playback source does not change;
- existing Ready variants remain intact;
- Master becomes available for retry with the same settings;
- next attempt gets the next sequential mastering number.

### US-11: User changes output format

Expected behavior:

- Output Format setting changes;
- Bit Depth visibility follows WAV/non-WAV rules;
- next job creates a file with the selected format and corresponding extension;
- format change participates in Master button state calculation.

### US-12: User opens another file after previous masters exist

Expected behavior:

- previous mastering session is cleared from UI;
- new file becomes Original track;
- numbering restarts from Mastering 1;
- old settings snapshot no longer controls Master button availability for the new source.

### US-13: User tries to open another file while mastering is running

Expected behavior:

- Open File is disabled until the active mastering job ends.

### US-14: User closes the app while mastering is running

Expected behavior:

- active mastering process tree is terminated through the PhaseLimiter engine;
- application can close cleanly without leaving FFmpeg/PhaseLimiter processes behind.

## 20. Acceptance criteria

The feature is complete when all of the following are true:

- Original track is created from the loaded source file;
- Mastering N variants are generated from Original, never from another mastering variant;
- all listed settings are represented by the required control types and mapped to the correct MasteringSettings/PhaseLimiter parameters;
- settings are arranged in the specified two-row panel between transport controls and waveform;
- variants are displayed in a vertical right-side panel;
- active mastering progress is visible in the corresponding item;
- mastering variants are not newly selectable while a job is running;
- Original remains usable during processing;
- changing settings during processing does not mutate the active job;
- Master availability follows settings snapshot equality and processing state;
- successful results automatically become selected and start at the current shared playback position;
- A/B switching preserves the shared position and starts playback immediately;
- waveform follows the selected variant;
- failed jobs remain visible without breaking the session;
- loading a new source resets the variant session;
- application shutdown terminates the active PhaseLimiter process tree;
- existing playback and waveform infrastructure is reused.
