# Research data, schema 2

The game still uses Firebase Auth and Firestore rules, without custom server
functions. UI, scoring, audio scheduling, controls and gameplay rules are unchanged.
Schema 1 records stay readable and are not rewritten. New sessions, trials and
notes use schema 2; consent receipts and profiles keep their existing schema.

## Identity and sessions

Document paths supply participant, session, trial and tone IDs:
`players/{firebaseUid}/sessions/{sessionUuid}/trials/{zeroBasedRound}/notes/{zeroBasedIndex}`.
Do not join by names or email. When exporting documents, retain these path IDs.
Consent names/contact information remains exclusively in consent receipts.

Session summaries retain game version, mode, score, highest level, checkpoint
revision, completion flag, and the consent receipt ID. That immutable receipt
supplies the decision and form version. New `startedAtUnixMs`, `endedAtUnixMs`
and `endReason` describe actual gameplay, using client UTC milliseconds. Server
`createdAt`, `updatedAt` and `savedAt` remain separate receipt timestamps.
An abrupt closure can leave the end time/reason null; never infer them from
`updatedAt`. Deliberately abandoned runs are ended but not counted as completed.

## Trials and conditions

Each consenting normal-game trial writes a `state: started` marker, then one
`state: resolved` result. A marker without a result is an incomplete observation,
not a timeout, incorrect answer, zero reaction time or successful completion.

The trial stores its level, correct quadrant, UTC start, chosen octave shift,
and effective settings: control layout, mode, note count/duration/gap, permitted
octaves, prediction window, comet delay/speed, round gap, advancement threshold
and weapon interval. Octave shift is constant across this game's sequence, so
it is stored once per trial. `wasPaused` and `lostFocus` identify affected trials;
`settingsChanged` reports settings differing at resolution from the start snapshot.
These observations do not alter the game state or scoring.

`firstDecision` and `secondDecision` contain the accepted shot's quadrant,
`atMs`, `mostRecentToneIndex` and `activeToneIndex`. An absent decision or active
tone is **null**, not -1 or zero. Quadrants and tone indexes are zero-based.
The second decision is specifically the first eligible fired shot following an
explicit switch away from the initial wrong lane, during the existing pre-spawn
window. Autofire in the initial lane and visually guided post-spawn recovery
do not become second guesses. Later ticks cannot replace the first second guess.

`predictionStatus` is `correct`, `wrong`, or `notAttempted`. `resolution` is
`Predicted`, `Destroyed`, `Hit`, `RadiationDeath`, or `Interrupted`. Spawn and
resolution have `cometSpawnedAtMs` and `resolvedAtMs`. Deliberate interruptions
identify logout, consent review, tutorial exit, application exit, or unavailable
audio. Browser shutdown callbacks are not guaranteed, hence the start marker.

## Timing and sound

Tone `startedAtMs`, both decision times, spawn and resolution share one research
clock: monotonic real elapsed milliseconds since trial start, excluding observed
`timeScale == 0` pauses. Pause changes are observed on frames; timing is not
sample accurate. The clock only records events and never drives the game.

These are **software playback-request and accepted-shot times**, not measured
acoustic onset, raw key-down times, or EEG synchronization markers. An active
tone means Unity's source reported playing, not proof the listener heard it.
An unfocused/stalled frame and audio-device latency can affect measurements.

Only initiated tones are saved. Each note retains requested frequency, effective
playback frequency/pitch ratio, clip/start frame/sample rate, gain, modeled peak
loudness, planned duration and whether resolution interrupted that duration.
The trial audio metadata retains calibration profile/model, master volume,
fade duration and modeled target. These describe digital settings; public
headphones are not calibrated SPL devices. Schema 2 replaces the old tone-relative
`requestedTimeInSequenceSeconds` with the common-clock `startedAtMs`.

From a decision's most-recent index you can derive the count of preceding tones;
subtract that note's onset from `atMs` for time since the latest tone. Keeping
those derived values out of the upload prevents contradictory duplicate fields.

## Saving and consent

The client and Firestore both require accepted current consent. Declining saves
only account/consent, tutorial completion, scores and session summaries. No
research recorder is started. Tutorial practice never creates a normal session
or trial. A consent change blocks pending research from the old receipt, even
if participation is later accepted again. Prior records are not silently deleted.

Stable session UUIDs, round indexes and note indexes provide retry-safe identity.
Finishing a trial uses a conditional atomic commit with all its notes. On a lost
response the client reads and compares the existing records before confirming
success. Conflicting content fails instead of overwriting a result.

Schema 2 accepts up to 100 planned/played notes, matching the saved level presets
and the existing Settings limit. Played note count cannot exceed the trial's
saved planned count. Schema 1 keeps its original 64-note limit.

Unacknowledged payloads are frozen in an account-scoped PlayerPrefs outbox, with
no credentials or contact details. Logging back into that account restores and
flushes them; another account cannot adopt the queue. Scores remain pending even
when research must be dropped because consent changed. Requests are removed
only after confirmation. Research uploads retain the existing 24-hour window.

PlayerPrefs is not encrypted and WebGL has a 1 MB PlayerPrefs limit. Backup
failure leaves requests in memory and shows an existing account-status message.
Do not clear browser data while saves are pending. Abrupt closure can lose the
current unfinished trial's tones: they are bundled at resolution, rather than
making a request for every tone. The start marker makes this missingness visible.

## Verification

`tests/rules.test.mjs` exercises the rules with verified end-user tokens against
local Auth and Firestore emulators, including null responses, malformed notes,
ownership, consent revocation, immutable results and 64-note atomic commits.
In signed-out Editor Play mode, `researchIntegrationChecks.run()` exercises
real SampleScene controls, calibrated playback, correct/wrong predictions,
recovery, pause timing, timeout/collision, deliberate exit and tutorial exclusion.
Only network failures and lost commit acknowledgements are injected; the
database commits are real emulator commits. Its reload test clears volatile
client state and reloads the scene, retaining the actual local outbox.

The existing `firebaseIntegrationChecks.run()` covers account/consent regressions.
No participant data or production consent decision is changed by these tests.

Verified on 2026-11-07: 18 emulator rule tests, 35 research checks in running
Unity Play mode, and the existing 31 account/consent regression checks passed.
The schema-2 rules were deployed to `comet-catcher`. Separate temporary live
accounts confirmed successful schema-2 trial/note writes and readback, one copy
after duplicate attempts, HTTP 403 for declined research/foreign/anonymous reads,
and successful declined-player progress saves. Those temporary live accounts and
their test documents were removed. Existing player data was untouched.
No new WebGL build was produced or browser persistence test performed for this
change; the earlier browser release checks remain necessary.
