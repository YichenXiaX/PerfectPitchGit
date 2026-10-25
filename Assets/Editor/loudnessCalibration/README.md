# Perfect Pitch: piano loudness matching

## What is being matched

The precise method name is **model-based peak loudness matching using the time-varying Zwicker method, ISO 532-1:2017**. We match one predicted peak value per note; we do not make every note's entire loudness curve identical.

| Term | Meaning in this implementation |
| --- | --- |
| Frequency (Hz) | Oscillation rate, related to pitch. |
| Fundamental and harmonics | The note's fundamental and higher-frequency components; all contribute to the modeled loudness. |
| Amplitude envelope | How the recording's amplitude changes during its attack, decay, and ending fade. |
| Gain | An amplitude multiplier. Half the amplitude does not necessarily mean half the perceived loudness. |
| dB SPL | Physical sound-pressure level, referenced to 20 micropascals; Unity volume does not measure it. |
| Phons | Loudness level relative to an equally loud 1,000 Hz reference tone. |
| Sones | The perceptual loudness quantity predicted by the model and used as the matching target. |
| Time-varying | Loudness is calculated throughout the note, rather than from a single frequency or average amplitude. |
| Interpolation | Estimating values between precomputed pitch, duration, and gain entries. |
| Provenance | Records identifying the source audio, import settings, software, and modeling assumptions. |

The stereo metric is calculated in this order:

```text
noteLoudness = maximum over time of ((leftLoudness + rightLoudness) / 2)
```

The channel histories are averaged before taking their peak. This is a project-specific summary, not the average of independently selected channel peaks or a complete binaural hearing model.

The pressure assumption of 0.2 Pa per digital sample unit maps digital RMS amplitude 0.1 to an assumed 60 dB SPL. This supplies the level-dependent model with a reference; it does not measure the player's output level. The free-field option is a reference sound-field assumption, not measured headphone equalization.

## Runtime target selection

```text
For the complete allowed note range at the chosen settings:
    target = lowest modeled loudness at the master-volume ceiling

For each played note:
    solve gain so that modeledNoteLoudness(gain) = target
```

The target includes the allowed clip/pitch intervals and relevant interior table knots. It is not recalculated from each randomly selected sequence. Conditions outside the profile or without a feasible solution fail explicitly instead of silently extrapolating. Master volume zero is handled as mute.

The shipped table contains 80,864 response values. Duration nodes span 0.1–3 seconds and gain nodes span −54–0 dB in 3 dB steps. Response values are natural-log sones, interpolated over log pitch ratio, linear duration, and gain in dB. The positive pitch limit of 3 is included in the model.

## Offline analysis details

The input is Unity-decoded float32 PCM. The generator resamples the planned pitched playback to 48 kHz, models its ideal end fade, and appends 0.2 seconds of silence for the time-varying analysis. It keeps stereo channels separate rather than averaging waveforms, which could cancel components.

Quiet lead-in detection uses 10 ms combined-channel RMS windows, approximately 1 ms steps, and a threshold 30 dB below the largest window in the first 500 ms. It searches the first 250 ms, retains 2 ms before the crossing window, and applies only offsets greater than 5 ms. One recording had about 90 ms of quiet lead-in, which otherwise made 100 ms notes nearly silent.

MoSQITo 1.2.1 supplies the model. Selected numerical loops are accelerated with Numba and verified against the unmodified implementation; Python is an offline tool only. ISO 226:2023 coefficients are not substituted into the ISO 532 algorithm.

## Integration and measured validation

The piano playback uses an offline loudness profile for the 88 assigned stereo recordings. `frequencyManager` selects a recording and pitch as before, then asks `pianoLoudnessCalibration` for a gain matched to the planned note duration. The profile is `Assets/Audio/loudnessCalibration/loudnessProfile.json`, assigned to the scene's **Loudness Calibration Data** field. Python and the loudness model are needed only to regenerate this asset; playback uses C# lookup and interpolation.

For each fixed condition, the runtime selects one feasible modeled loudness target across all allowed frequencies and selected octaves. Duration, fade, octave selection and `masterVolume` define that condition. The master value is a source-volume ceiling: gains are solved at that level, without a second master multiplication or independent per-note clipping. Changing the condition recomputes its target. The default duration is 0.59 seconds, fade is 0.15 seconds and master ceiling is 0.8.

The profile models MoSQITo 1.2.1's time-varying Zwicker method under [ISO 532-1:2017](https://www.iso.org/standard/63077.html). It analyzes each stereo channel separately, averages the loudness histories, and takes their peak. This statistic is not a binaural or individual-hearing model. The assumed scale is 0.2 Pa per digital sample unit; it does not measure the listener's SPL. The separate `pianoLoudnessCompensation` utility implements [ISO 226:2023](https://www.iso.org/standard/83117.html) continuous pure-tone reference contours. It is reference-only for this piano path: do not apply it on top of the ISO 532 piano gains, which would count frequency sensitivity twice.

Quiet lead-in offsets are recorded for 54 of the 88 clips. Playback starts at the modeled offset on the first cycle; subsequent loops use the complete original recording. The WAV files are unchanged. The offset is converted to the runtime clip's sample rate, and the session records that rate and start frame.

The shipped profile `iso532-1-unity-stereo-56a82465c12154a4` passed these numerical checks:

| Check | Result |
| --- | --- |
| C# condition construction | 1,040 conditions; zero failures |
| C# note gain checks | 242,480 notes; zero failures |
| Fresh underlying-model evaluation of C# gains | 1,000 cases; maximum relative target error 3.4867% |
| Default 0.59-second / 0.8-master condition | 165 model evaluations; maximum relative target error below 0.6632% |

These are errors against the modeled target in the tested cases, not measurements of listener perception or an exhaustive ISO conformance certification. The profile includes its ID, model and software versions, pressure assumption, grid, onset map, validation summaries, and source/import/decoded-PCM hashes. Session records retain the profile ID, condition, requested frequency, effective pitch and gain, planned peak sones, and whether the result arrived before the planned note end.

## Regenerating the profile

1. Open the scene containing the assigned piano clips in Edit mode. Choose **Tools > Export Piano Loudness Inputs** and select a dedicated folder outside `Assets`. This exports decoded PCM and a provenance manifest without playback or source edits.
2. Follow the offline tool instructions in `Assets/Editor/loudnessCalibration/tools~/README.md` to install the pinned environment, validate the model, analyze onsets and generate a replacement profile.
3. Replace the profile asset, preserve its scene assignment, and run **Tools > Validate Piano Loudness Profiles**. The manual command checks already-open enabled build scenes and reports unopened scenes it skipped. During a build, the validator checks every included scene as Unity processes it, without opening extra scenes. It rejects missing profiles or changed sources, importer metadata, dependencies, dimensions, Unity version, build target, onset offsets or fade settings. Recheck playback in the intended player after regeneration.

The analysis uses an ideal continuous linear fade and offline resampling. Actual playback retains the existing per-frame coroutine fade and frame-based timing; these can differ, especially for short notes or slow frames. Unity WebGL also uses browser decoding and resampling, so Editor-exported PCM is an approximation of browser output. Build validation establishes input provenance, not identical output waveforms or acoustic timing. See [Unity's Web audio documentation](https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-audio.html).

Public-device sessions remain explicitly `uncalibratedDevice`: headphones, speakers, system volume and hearing differ. For future controlled EEG work, measure the complete equipment chain, establish the physical SPL reference and channel response, and validate onset/offset timing and synchronization with recorded audio and experiment triggers. The current planned loudness values and logical elapsed times are not measured SPL or EEG onset markers.

The portable C# regression projects are in `tools~/runtimeTests` and use .NET 8. From that folder:

```powershell
dotnet run --project calibrationRegression.csproj
dotnet run --project isoReferenceRegression.csproj
dotnet run --project realProfileRegression.csproj -- ../../../../Audio/loudnessCalibration/loudnessProfile.json ./validation-output
```

The last command writes the solved cases used by the offline `validateSolvedGains.py` waveform check. The tool bundle manifest describes the original calibration bundle; its profile file is installed at the runtime asset path given above. Model test data and tools stay in the Unity-ignored `tools~` directory.

## Files added and changed

This inventory covers the loudness work only, not the earlier camelCase refactor, held-key shooting feature, or original replay repair. Paths are relative to the Unity project root.

### New runtime files

| File | Purpose |
| --- | --- |
| `Assets/Scripts/pianoLoudnessCalibration.cs` | Validates and copies profile data, interpolates responses, selects the common target, and solves gains. |
| `Assets/Scripts/audioLoudnessRecord.cs` | Defines serializable `audioLoudnessRecord` and `notePlaybackRecord` data. |
| `Assets/Audio/loudnessCalibration/loudnessProfile.json` | Generated responses and provenance for all 88 recordings. |

### New Editor files

| File | Purpose |
| --- | --- |
| `Assets/Editor/loudnessCalibration/pianoLoudnessExporter.cs` | Adds **Tools > Export Piano Loudness Inputs**; exports decoded PCM and its manifest. |
| `Assets/Editor/loudnessCalibration/pianoLoudnessBuildValidator.cs` | Adds manual validation and validates scenes during build processing, without opening extra scenes. |
| `Assets/Editor/loudnessCalibration/README.md` | This implementation explanation and inventory; expanded in the documentation follow-up. |

### Existing files modified

| File | What changed for this feature |
| --- | --- |
| `Assets/Scripts/frequencyManager.cs` | Replaced fundamental-only compensation with profile-based gain solving; added validation, fixed-condition targets, lead-in offsets, browser sample-rate handling, and audio records. Removed unused `minCompensationVolume`, the obsolete coroutine handle, and overwritten/capped volume calculations. Logs the current gain after applying it; stops its audio coroutines during cleanup. |
| `Assets/Scripts/pianoLoudnessCompensation.cs` | Rewritten as an ISO 226:2023 pure-tone reference with input validation and fixed-domain normalization. Retained for reference; not used in the piano playback path. |
| `Assets/Scripts/gameManager.cs` | Validates the profile/condition before start, replay, and rounds; attaches audio metadata to sequence results. |
| `Assets/Scripts/gameSessionData.cs` | Added `audioLoudness` to sequence-result data. |
| `Assets/Scripts/gameOverUi.cs` | Hides the game-over panel only when restart succeeds, preserving access to the panel if calibration fails. |
| `Assets/Scenes/SampleScene.unity` | Assigned the generated profile to `loudnessCalibrationData` and removed the obsolete serialized `minCompensationVolume`. |

### Offline tools and tests

These live in `Assets/Editor/loudnessCalibration/tools~/`. The trailing `~` makes Unity ignore the directory; the tools and test fixtures are not a player dependency.

- Generation: `generateProfile.py`, `fastModel.py`, `analyzeOnsets.py`.
- Independent verification: `validateModel.py`, `validateSolvedGains.py`.
- Setup and documentation: `requirements.lock.txt`, `README.md`, `.gitignore`.
- Attribution: `LICENSE-MOSQITO.txt`, `THIRD_PARTY_NOTICES.txt`.
- Reproducibility: bundle/reference manifests, per-clip onset data, numerical validation reports, and naming-equivalence records.
- `reference/`: upstream test-10 audio fixture and expected values.
- `runtimeTests/`: three .NET 8 regression programs/projects for the calibration math, ISO reference, and actual generated profile, plus reference values and `NuGet.Config`.

The full source/report file list is provided below. Generated `bin/`, `obj/`, caches, and scratch outputs are working artifacts and are excluded from this list.

Unity-generated `.meta` files accompany the new imported scripts, profile, documentation, and folders. Existing script GUIDs were preserved. The original piano WAVs and their import settings were not changed.

## Verification scope

In addition to the profile checks above, 13 calibration regression suites (8,730 assertions) and 10 pure-tone reference suites (6,755 assertions) passed. Unity Editor compilation, playback starting position, short-note playback/replay, repeated game-over/replay, and saved audio metadata checks passed. A browser build/playback test was not completed.

The reported 0.6632% default-condition error is disagreement with the selected numerical model in tested cases, not measured equality of human perception. A newly generated table must be validated again; it does not inherit that measured accuracy.

## Offline file list

```text
.gitignore
LICENSE-MOSQITO.txt
README.md
THIRD_PARTY_NOTICES.txt
analyzeOnsets.py
bundle-manifest.json
endpoint-validation.json
fastModel.py
generateProfile.py
identifier-map.json
identifier-rename-equivalence.json
model-validation.json
onset-proposals.json
onset-short-note-check.json
reference-fast-equivalence.json
reference-manifest.json
reference-validation.json
reference/test10-values.json
reference/test10.wav
requirements.lock.txt
runtime-model-validation.json
runtime-validation-cases.json
runtime-validation-report.json
runtimeTests/NuGet.Config
runtimeTests/calibrationProgram.cs
runtimeTests/calibrationRegression.csproj
runtimeTests/iso226-2023-reference.json
runtimeTests/isoReferenceProgram.cs
runtimeTests/isoReferenceRegression.csproj
runtimeTests/realProfileProgram.cs
runtimeTests/realProfileRegression.csproj
slopes-fast-validation.json
validateModel.py
validateSolvedGains.py
```
