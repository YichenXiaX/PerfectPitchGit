# Offline nominal loudness calibration

The runtime table is a prediction of a specified playback signal, not a
measurement of sound pressure at a listener's ear. It retains the stereo piano
recordings. Public devices, headphone response, personal hearing and Unity's
actual resampler/fade timing can change perceived loudness.

## Model and stimulus

- MoSQITo 1.2.1, ISO 532-1:2017 time-varying Zwicker, free field.
- Analyze decoded stereo channels separately; average their loudness time
  histories, then take the maximum. This is an event statistic, not a binaural
  loudness model.
- Assume 0.2 Pa per digital sample unit. A digital RMS of 0.1 therefore
  corresponds to an assumed 60 dB SPL, using 20 micropascals as reference.
- Analyze at 48 kHz after scipy polyphase resampling. The pitch-resampling
  ratio is represented by a fraction with denominator at most 2048.
- The first playback cycle begins at the recorded playbackStartFrame; later
  loops begin at frame zero. Source audio files are not modified.
- Apply the existing ideal linear end fade, bounded by note duration, then
  append 0.2 seconds of silence for the time-varying model.
- Quiet lead-in selection uses 10 ms combined-channel RMS windows, a 1 ms
  step, and a threshold 30 dB below the largest window in the first 500 ms.
  Search the first 250 ms, retain 2 ms before the crossing window, and only
  apply a skip greater than 5 ms. The complete per-clip map is recorded.
- Store natural-log sones versus log pitch, linear duration and gain dB.
  The runtime solves gain against a common feasible target across the selected
  frequency domain at the current master volume; it does not post-scale a
  gain solution computed at another acoustic level.

## Reproduction

Use an isolated Python environment and install requirements.lock.txt.
The generator is an offline build tool, not a Unity runtime dependency.

    python validateModel.py
    python analyzeOnsets.py --decoded-dir decoded --output onset-proposals.json
    python generateProfile.py --decoded-dir decoded --onsets onset-proposals.json --output loudness-profile.json --workers 12

Run the reference check in the actual environment before rebuilding. The
profile's reference-validation flags describe this validated implementation
baseline; preserve the current environment's validation report alongside a
regenerated profile. The shipped gain axis extends from -54 dB to 0 dB in
3 dB steps.

Optional switches include --cache-dir, --reuse-profile, --durations,
--gain-step-db, --minimum-gain-db, --minimum-frequency and --maximum-pitch.
The decoded directory contains the Unity-exported manifest and raw float32
PCM files. Their SHA-256 hashes are verified before modeling. The profile
contains portable source/import/decoder provenance, not user paths.

## Numerical acceleration and validation

fastModel.py scalarizes two upstream numeric loops for Numba. It retains
algorithm constants, rounding, thresholds, update order and temporal sampling;
fast-math is disabled. Other stages call the pinned MoSQITo implementation.
Gain cache reuse removes the fixed numerical floor before scaling power, then
restores it; merely adding gain in dB to the floored spectrum is incorrect.

Validation artifacts distinguish:
1. Official upstream test-10 pulse values compared with the untouched API.
2. Direct API versus accelerated total and specific histories.
3. Held-out response-grid interpolation, including tiny domain padding.
4. Fresh underlying-model evaluation of gains actually solved by the C#
   runtime for stated conditions.

Passing these tests is not exhaustive ISO conformance certification, nor a
claim of calibrated public-device perception. For controlled EEG sessions,
measure the complete output chain with the selected headphones and establish
the SPL reference, channel balance, timing and any needed device correction.

See THIRD_PARTY_NOTICES.txt, LICENSE-MOSQITO.txt and reference-manifest.json
for provenance and attribution.

