# HELIOS launch presentation

`gameMenuTerminal.start` installs this presentation only in SampleScene, reusing
the existing Start button and its settings callback. No saved scene, prefab, Aura
HUD, player controls, account routing, consent logic, or tutorial UI is restyled.

`launchSequence` waits one frame before widening the camera so the player controller
can cache its original lane geometry. Its states are initializing, ready, launching,
connecting, failed, playing, and ended. The normal entrance has a 0.42-second charge,
the approved 2.2-second camera move, and a 0.18-second settling interval. Replay and
failed-request retries use a 0.35-second transition. Inspector fields tune the charge,
camera duration, and launch volume; these are not player difficulty settings.

The first round starts through the existing gameManager callback only after the
camera has landed and cloudAccount confirms the session. Session creation happens
after the animation, so introduction time is excluded from the run. Repeat clicks
are ignored. Failed requests expose the provider's existing sanitized message and a
Retry launch button; cloudAccount retains its existing idempotent request UUID.
Connection status appears only after a noticeable wait, avoiding a brief label flash
on fast connections. Blocking overlays and browser focus loss pause the sequence.

`terminalTransmission` reveals a fixed text layout, with punctuation pauses and a
blinking cursor. It owns its glow material; it does not alter the shared font material.
Start remains available while text types.

`starfieldBackground` maintains one particle field across the entire camera path,
including smaller aspect ratios. Its private PRNG never consumes gameplay random
numbers. The launch uses unscaled presentation time; gameplay returns to scaled time.
Particles are retained across the handoff. `heliosEngineGlow` adds two aft exhaust
meshes behind the existing sprite, leaving the hull's tint and animation untouched.
`launchEdgeGlow` renders below the original HUD. Generated meshes, materials, textures,
and the short filtered-noise launch clip are released with their owning scene objects.
The launch sound stops before the first gameplay note.

Editor verification: enter Play mode, then call `launchChecks.run()`. This uses an
Editor-only mock transport and creates no real participant records. It checks the
real menu callbacks, camera/field continuity, early held inputs, duplicate requests,
connection failure/retry, overlay pause, Replay, tutorial isolation, and scene return.
`launchChecks.movie()` records rendered frames after the checks for visual review.
The test transport is not compiled into a player build.
