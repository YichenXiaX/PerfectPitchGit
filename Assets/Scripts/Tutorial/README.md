# Flight training and Signal Terminal

The normal gameplay scene and Aura/level HUD are preserved. `flightTraining.unity` contains a copy of the saved game environment and references, plus a small `tutorialDirector` state machine. It uses the existing controller, calibrated piano playback, ship/comet prefabs, laser, celebration, health bar and Aura feedback.

## Files changed in the clarity/polish pass

- Tutorial behavior: `tutorialDirector.cs`, `practiceTarget.cs`; new `tutorialSpectrum.cs` and `tutorialPitchConnections.cs`.
- Shared gameplay paths: `frequencyManager.cs` adds isolated multi-note practice playback; `bulletBehavior.cs` removes the tutorial durability exception; `radioactiveDamage.cs` adds a practice-only health floor. `gameSettings.cs` and `settingsUi.cs` normalize and expose five octaves.
- Scoped UI: `signalTerminal.cs`, `terminalKeycap.cs`, `terminalPanel.cs`, `terminalScrollFocus.cs`, `terminalSettingsView.cs`; new `terminalFontSet.cs`.
- Assets: both `SampleScene.unity` and `flightTraining.unity` lose obsolete octave controls. New Barlow Regular/Medium and Noto Music sources/licenses live under `Assets/UI/SignalTerminal/Fonts`; new TMP/fallback/reference assets live under `Assets/Resources/SignalTerminal`.
- Editor-only setup/checks: updated `terminalFontBuilder.cs` and `flightTrainingChecks.cs`; new `tutorialPolishSetup.cs` and `tutorialPolishChecks.cs`. The setup tool refuses to replace a dirty scene. This README describes the revised behavior.

Account/backend scripts, consent wording, calibration assets and the protected HUD are unchanged by this pass.

## Controls

`controlLayout` is the shared source for Settings, account preferences, tutorial prompts and gameplay. Right hand is P / [ / ] / backslash; left hand is the top-row 1 / 2 / 3 / 4. F fires in both. The preference uses PlayerPrefs and survives application/browser restarts on that device. It is separate from account onboarding and is not a cloud-synced preference.

`playerController.shotInterval` defaults to 0.20 gameplay seconds, with a 0.05-second minimum. The currently selected lane key and F must both remain held. InputAction callbacks preserve key-event order. Cooldown survives releases, lane changes and overlays; slow frames emit at most one shot. A first prediction needs a fresh F press after each round, replay, blocking overlay or layout change. An unresolved wrong prediction permits lane changes while continuing to hold F.

A second guess means the FIRST eligible backup shot fired after an explicit change to a lane different from the original wrong prediction, while the pre-spawn window is still open. Same-lane automatic shots do not consume that opportunity. Later shots cannot overwrite it, and post-spawn shots are recovery, not second guesses. The initial prediction and response-time fields are retained. Normal-game trial uploads now require account-owned research consent; practice never uploads them. Guided tutorial choices do not establish learning or above-chance performance.

## Tutorial isolation and completion

Required actions are a guided correct prediction, a planned wrong prediction, switching lanes and destroying the comet with one projectile hit, releasing F for a real lethal collision, activating the existing Replay action, and an independent sound-to-quadrant attempt. Errors on the final attempt offer a hint and replayable cue. Only the final correct interception enables completion. Text acknowledgements do not substitute for required gameplay events. There are no simulation shortcuts in the player build; keyboard injection exists only in Editor verification code.

Practice uses no normal game-loop coroutine, creates no server game session, writes no game checkpoints or research records, and does not advance normal levels. There is no reinforced target or hit counter: the first real bullet contact uses the normal destruction/effect/event path. Held-fire cadence is tested separately without adding health to comets. The standard 100-damage impact rule is unchanged. Replay resets practice and starts the independent sound trial, without repeating earlier lessons.

Both recovery and impact use the actual Q4 prefab. `practiceTarget` only stages movement: the comet begins above the camera edge using rendered bounds, descends at 2.4 world units per second and holds fully visible near the top. Input, radiation and collision are disabled during entrance. The director waits for the existing Aura number/floating feedback and vapor particle lifetime to settle, then freezes and spotlights the comet. HUD/VFX timings are not changed. Resume clears stale input. Recovery uses a tutorial-only movement stop above the hull and radiation floor of 80 health to prevent an accidental lesson death. Normal gameplay retains its original damage. The impact lesson blocks firing and lets the comet travel into the real collider; the actual explosion has 2.5 seconds before the Replay panel appears.

## Sound and pitch teaching

`tutorialSpectrum` reads the four ranges from `frequencyManager.quadrants` and the four actual sprites from `gameSettings.cometPrefabs`. Its reversed C5-to-C4 piano is a musical reference. The separate logarithmic spectrum uses the real approximately 254.29–508.57 Hz range; connectors link white-key reference frequencies to that scale. C5 lies beyond the upper gameplay boundary. Markers, category cards and comet previews highlight as notes play. The narrow layout scrolls the body while retaining visible actions and clearance around the unchanged HUD.

`frequencyManager.playPracticeSequence` plays three deterministic, distinct pitches at positions 0.24, 0.69 and 0.43 through a quadrant's logarithmic range. Initial examples use octave zero; the later comparison plays Q2 at 0, +1 and -1, in that order, with matching on-screen labels. All three teaching octaves share one calibrated loudness target. Tutorial notes last 0.65 seconds with 0.12-second pauses, using the existing sample selection, loudness gain, fade and pause path. These local overrides do not change saved presets or calibration. Replay cancels the preceding sequence; leaving a step stops audio and stale highlighting. Continue stays disabled until all three versions finish. Input is disabled until each required cue finishes. A quadrant is a sound category; its lane is the matching screen position.

Settings now contains exactly −2, −1, 0, +1 and +2. Obsolete controls, serialized fields and load/save references are removed in both gameplay and training scenes. Old presets retain supported, distinct values in sorted order; an empty result falls back to zero. Preset selection and custom settings use the same normalization.

The existing Settings object also contained scene-specific UI. Its data is now kept on a separate persistent object; the original scene UI and its serialized references remain scene-local. This prevents destroyed-panel references after training, scene reload or logout. No HUD integration or styling changes were required.

Completion is stored in the authenticated Firebase account profile as `tutorialVersion` (current version 1). Firestore rules require a verified owner, a saved consent decision and the supported tutorial version. This is client-reported onboarding, not an anti-cheat attestation. The rules also gate normal session creation on completion. Exit training returns to the account screen without signing out or completing unfinished training. Replay does not erase prior completion. The final **Enter game** action saves completion and enters the normal start flow only after that save succeeds. A failed save displays the error and leaves the action available to retry. Active runs must save successfully before exiting to training.

## Backend

Firebase rules are deployed to `comet-catcher`. See `Backend/firebase/README.md`
for the current setup, deployment command and verification status. There is no
machine-global or silent local completion fallback. Old provider accounts/data
were not imported; the owner chose a fresh start.

## UI scope

The reconstruction uses the approved HTML's **Signal Terminal** variant. It replaces the earlier wide chat-style panels with bounded navy panels, fine terminal rules, compact actions, explicit selection states and individual keycaps. It does not use the reference's simulated controls, sample consent copy or replacement HUD.

The authoritative visual owners are:

- `signalTerminal`, `terminalPanel`, `terminalFrame`, `terminalFocus` and `terminalKeycap`: scoped tokens and reusable panel/control geometry. Headings reference the existing Science Gothic asset through `terminalFontSet`, without modifying it. Prose uses Barlow Regular and buttons Barlow Medium. Dedicated Noto Music and Liberation Sans fallback assets supply note symbols and arrows. Static fonts and OFL licenses are bundled from [official Barlow](https://github.com/jpt/barlow) and [Noto Music](https://github.com/google/fonts/tree/main/ofl/notomusic). Older saved consent font references remain valid.
- `terminalLayoutSelector`: the same two-layout selector in training, account and Settings. It refreshes when reopened, so selections made elsewhere remain visible.
- `tutorialDirector`: compact lesson content and one final Enter game action. Required gameplay events, practice Replay, audio and effects remain connected to the existing systems.
- `accountTerminal`: bounded account/login/verification presentation with original callbacks, readable status/error messages, compact statistics and one Account/exit entry point. `accountUiBuilder` supplies stable serialized bindings; the runtime view owns layout.
- `terminalSettingsView` and `gameMenuTerminal`: scoped Settings and start/game-over presentation. Original settings values, serialized toggle, buttons and callbacks are retained.
- `consentTerminalTheme` and the saved `researchConsent.unity`: full document content inside a scrollable body with a fixed action footer. Original form wording, fields and agreement/decline conditions are retained. Keyboard focus scrolls fields into view. The November consent integration includes this scene in builds and saves the choice before training; see `Backend/firebase/README.md` for the new account flow and deployment status.

New UI canvases use a 1280x720 reference with height matching: one reference pixel maps to `canvasHeight / 720` display pixels. Panels have bounded preferred widths and responsive viewport caps; the opening is 620x359 reference pixels. The WebGL template reserves 38 browser pixels for its footer, so its canvas is shorter than the browser viewport. The existing HUD CanvasScaler and all protected transforms remain unchanged. Tutorial/menu keyboard navigation yields to account and Settings overlays.

There are no backend, schema or gameplay rule changes in this visual reconstruction. The earlier tutorial deployment described above is still a separate requirement.

## Verification

- `heldFireChecks.run()` passes 16 assertions and exercises no-lane selection, release latches, cooldown, re-press/lane changes, stalled frames and minimum intervals.
- `flightTrainingChecks.run()` passed 116 checks at 1280×720 and again at 800×600 using actual Unity scenes, physics and injected keyboard events with an Editor-only mock transport. It covers both layouts, three distinct calibrated notes per quadrant, preset preservation, required lessons, first-contact destruction, correct/wrong predictions, second-guess capture, timing, pause, timeout, game over/replay, the final independent attempt, scene interruption, account switching and failed saves. It is excluded from player builds.
- `tutorialPolishChecks.staticChecks()` checks legacy octave filtering, nonempty fallback, duplicates, five serialized toggles, existing heading references and required glyph availability. Rendered polish captures include 1280×720, 1920×1080 and 800×600: piano/comet mapping, a sounding-pitch highlight, settled Aura/spotlight and five contained octave controls. The narrow teaching and Settings panels were corrected after visual inspection to preserve HUD/Account clearance.
- Timestamped runtime frame sequences record the above-screen arrivals, one-hit recovery and real collision/game-over sequence. They show Aura feedback settling before freeze; these are actual Unity captures, not HTML simulations.
- Account integration checks pass 12 authentication/save checks and 8 gameplay/pause/replay/logout checks using the Editor mock transport. Targeted checks also cover layout selection after reopening hidden panels, focus clearing and blocking account backgrounds.
- Consent navigation passes 6 Editor checks, including Tab focus scrolling and agreement-dependent Continue state. Native keyboard Enter was separately checked in the Editor to toggle agreement on and off. No form was submitted.
- All 19 backend tests and TypeScript checking passed during the earlier tutorial implementation. They execute the actual SQL migrations with PGlite and exercise the HTTP handler, including ownership, invalid completion receipts and the server onboarding gate. Backend code is unchanged in this visual pass.
- Rendered Editor checks cover opening, spectrum, both input layouts, prediction/success/miss, spotlight/recovery, second-guess tip, impact/game-over, completion, login/account/error states, Settings, start/game-over menus and all consent forms. The opening was compared directly with the rendered Signal Terminal reference at 1280x720. Viewport checks include 1280x720, 1920x1080, 800x600 and 1920x800.
- For this polish pass, all 38 protected HUD objects/components in each of SampleScene and flightTraining match the pre-edit baseline. `auraHud.cs` and the Science Gothic font source/asset hashes are unchanged. The scenes changed only for the requested octave controls and preset normalization; the whole scene files therefore differ.

`terminalVisualChecks` provides Editor-only repeatable captures and navigation checks; its default output directory is `Temp/SignalTerminalVerification`. It does not ship with the player. Mock account values and input injection are verification tools only.

The final polish WebGL build succeeded (28,956,700 bytes; zero errors). It reported two vector-truncation warnings in Unity URP's package-owned `Hidden/Light2D` shader. The rebuilt login screen was reloaded and visually inspected in the browser with the new fonts; the browser console had no warnings or errors. This smoke check does not establish a live authenticated tutorial or browser-audio pass. The Editor was restored to the original flightTraining scene and Game-view selection, with Play stopped, timeScale 1 and audio unpaused.

Known limitation: the original health HUD clips below the viewport at the unusually wide 1920x800 aspect ratio. Its pre-existing geometry was deliberately preserved, as requested; the new panels remain bounded. The health display is visible at the other tested sizes.

The November 7 Firebase migration completed the interactive tutorial in a local
WebGL player using the Auth/Firestore emulators, then confirmed saved completion
after a browser reload and login. Live Firebase consent/account tests and a real
verification email also passed. Browser gameplay/audio still needs the release
checks described in `Backend/firebase/README.md`. The public build retains
normal authentication; no testing shortcut was added.
