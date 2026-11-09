# Firebase migration

This is the rules-only Firebase path: Firebase Authentication, Cloud Firestore,
and static Firebase Hosting. It has no Cloud Functions, Extensions, Analytics,
Admin SDK dependency in the game, or billing requirement introduced by this code.

## Current status

The client is connected to live project `comet-catcher`. Its default Firestore
database is Standard/Native in Iowa (`us-central1`), with reviewed rules and indexes
deployed. Billing is disabled. `Assets/Resources/firebaseConfig.json` contains
only the public project ID and API key. The old provider code is removed. The
owner chose new Firebase accounts; old Supabase accounts/data stay untouched.

The screens, consent wording, tutorial lessons, gameplay, and Aura HUD are reused.
Unity talks to Firebase's REST APIs because the native Firebase Unity SDK does
not support WebGL. Tokens and passwords are never logged. Login tokens stay in
memory; reopening the game requires login again. This does not resume a partially
played run. Saved scores and completed onboarding are loaded from the database.

## Data and permissions

| Location | Purpose |
| --- | --- |
| `players/{uid}` | Nickname, current consent receipt, tutorial version/completion |
| `players/{uid}/consents/{requestId}` | Immutable approved-form decision/details and server timestamp |
| `players/{uid}/sessions/{sessionId}` | Run summary, ordered checkpoints, completion flag |
| `.../sessions/{sessionId}/trials/{roundIndex}` | Consent-gated sequence result and audio metadata |
| `.../trials/{roundIndex}/notes/{index}` | Exact requested frequency and playback settings for one note |

Only a verified email/password account can access its own records. All other
client access is denied. Firebase UIDs are pseudonymous participant identifiers,
not anonymous identities: administrators with Auth access can associate them
with email accounts. Consent names/contact details are stored only in receipts;
trial/note schemas reject extra fields, including copied identity details.

Declining still permits ordinary session summaries and progress. Research writes
must refer to a session created under the **current** accepted consent receipt.
Changing consent immediately prevents uploads from older sessions, even after
agreeing again. Existing historical records are not automatically deleted.
The child/parent agreement and follow-up conditions mirror the current form.

Consent receipt creation and the current-choice pointer are one atomic commit.
Each run has one stable UUID. Checkpoints increment a revision and use Firestore
update-time preconditions. A trial-start marker becomes a finished result once;
that result and its notes are committed together and are then immutable.
After a lost response, retries compare the already-saved record and confirm it;
they do not create another record or silently overwrite a changed record.

Tutorial completion is account-specific and versioned. The unchanged tutorial
calls the save operation after its required final action. Rules require consent
before completion and training before a normal session. As before, the client
reports tutorial milestones and scores; this is not server-proven gameplay or
an anti-cheat system. Tutorial practice never opens a normal cloud session.

Note records contain intended digital playback settings, not a measurement of
sound reaching the listener's ear. Schema 2 records explicit octave shift, decision
times/context, trial conditions and incomplete-trial markers. See
[the research data dictionary](RESEARCH-DATA.md). Each stored document has a schema version. A new
game, consent, or tutorial version must update the matching rule checks.

## Local tests

Install Node.js and Java 21, then from this folder:

```sh
npm ci
npm test
```

The tests use only `demo-comet-catcher` and loopback emulator addresses. They
exercise requests carrying end-user tokens, not privileged database writes.
The first suite passed 13 tests covering ownership, verified email, consent,
child/parent conditions, tutorial gates, declined-player saves, ordered
checkpoints, immutable retries, schema validation, and a 64-note atomic trial.

For the Unity integration check, start both emulators:

```sh
npx firebase emulators:start --project demo-comet-catcher --only auth,firestore
```

In a signed-out Editor Play session run `firebaseIntegrationChecks.run()` through
the Unity CLI. The final suite passed 31 checks using the real forms and REST
adapter, including expired access-token refresh, invalid verification links,
response loss after real database commits, returning login, scores, consent
changes, and tutorial replay. Email delivery uses the emulator mailbox;
this does not prove delivery to a real inbox. Tutorial persistence is tested
directly here, not a new rehearsal of every physical tutorial lesson.

## Live verification and updates

Two temporary live accounts exercised the deployed rules with end-user ID
tokens: own consent/training/session/trial/note writes succeeded; anonymous and
cross-account reads were denied; repeated trials could not create duplicates;
decline blocked research while allowing scores. Unity also confirmed returning
login and new-account consent routing against the live project. The owner
received a real verification email, confirmed it names Comet Catcher, and used
Firebase's hosted link successfully. Unity then logged that account in and
opened consent. Admin credentials were used only in external fixture setup and
cleanup, never in the game or permission assertions.

After the retired provider source was removed, an actual normal run in the
Editor launched against Firebase, ended through a natural comet collision, and
saved its final checkpoint and research result. An independent end-user read
confirmed one completed run, one trial and 15 note records with exact requested
pitch/playback fields. Temporary live test accounts were removed after testing.

Auth uses the default Google email service and project-name template. Custom
sender/subject edits were rejected by the provider and were not applied. The
actual password policy was checked: 6–4096 characters, no composition requirement.
No `localhost:3000` redirect or bearer-token URL callback remains.

For future rule updates, after Firebase CLI login, from this directory:

```sh
npx firebase deploy --only firestore:rules,firestore:indexes --project comet-catcher
```

Review and test changes before deploying. Permissive test-mode rules are not
needed. No service-account key belongs in this directory or a Unity build.

## Hosting

`buildWebRelease.csx` builds the four enabled scenes to this folder's ignored
`public` directory. It refuses Play mode, unsaved scenes and custom scripting
defines. It builds without development/debug options, using hashed asset names
and Firebase's automatic transport compression. It preserves the audio imports,
calibration, scenes and gameplay.
The WebAssembly compiler uses Runtime Speed optimization; this is set for every
build because Unity stores that setting per machine.

From the project root, with the project open in Unity and the Unity CLI installed:

```powershell
$code = Get-Content -LiteralPath 'Backend/firebase/buildWebRelease.csx' -Raw
unity command script eval --project-path . --caller plugin --skill optimize-web --code $code --timeout 60 --format json
```

The command schedules the build. Wait for `.build-report.json` in this folder
to report `Succeeded` with zero errors before deploying; `queued` only means
Unity accepted the request. The report and generated player are gitignored.

`firebase.json` supplies JavaScript/WebAssembly MIME headers. All hashed files
under `Build` use immutable caching. HTML and other assets require cache
revalidation (`no-cache`), so a new deployment selects its own matching build
files. Do not disable hashed names while keeping the immutable-cache rule.
Upload normal `.wasm`, `.js` and `.data` files: Firebase chooses the delivery
encoding. A preview test found that precompressed Unity `.br` files were served
without the requested `Content-Encoding` header and could not load. No custom
decompressor or change to the audio codec is needed with CDN compression.

Firebase Hosting provides HTTPS. No catch-all rewrite is needed; Firebase's
reserved `/__/auth/action` route remains available for email links. A local test
player may explicitly define `COMET_FIREBASE_TEST` to use loopback emulators.
**Never define that symbol in a deployed build.** Do not deploy a local test
player or a build with incomplete public Firebase configuration.

Deploy a temporary preview from this directory after Firebase CLI sign-in:

```sh
npx firebase hosting:channel:deploy release-candidate --expires 7d --project comet-catcher
```

This only deploys Hosting. It does not redeploy database rules or enable billing.
The preview URL is accessible to anyone who has the link and uses the real
Firebase backend. Use designated test accounts. After the preview passes review,
promote exactly that tested version to the main site:

```sh
npx firebase hosting:clone comet-catcher:release-candidate comet-catcher:live --project comet-catcher
```

The earlier development build's missing comet shader and calibration mismatch
have been fixed. The browser now uses a measured Web Audio calibration profile;
the Editor profile remains unchanged. Independent audio checks and a complete
local browser tutorial/gameplay/replay journey passed. That local journey used
emulators; it does not establish a hosted production-backend run. See the
calibration README for measurement results and their limits.

### Hosted release verification — 2026-11-09

The separate hosted test used the real Firebase backend and the non-development
release, with no emulator or diagnostic scripting defines. The temporary preview
is [release-candidate](https://comet-catcher--release-candidate-gdgge82a.web.app),
Hosting version `6984204cc7e9a6e9`. It expires on 2026-11-16 at 22:08 UTC.
The main/live channel is unchanged. Preview links are public to anyone with the
link; this is not an access-controlled private environment.

- HTTPS, MIME types, native CDN compression, cache headers and the reserved email
  confirmation route passed. Measured Brotli transfer of the four build files
  was 32,057,235 bytes; the unpacked export is about 93 MB.
- The hosted Chromium player completed consent, all tutorial interactions,
  tutorial impact/restart, normal gameplay, held firing, game over and replay.
  Tutorial completion remained zero through practice reset and changed to
  version 1 only after Enter game. Training created no game sessions.
- One consenting run saved a completed session, one schema-2 trial and 14 note
  documents using `iso532-1-browser-stereo-e4afbfbc5af56085`. Two declined runs
  saved completed sessions and scores, with zero trial/note documents. Note IDs
  were unique and contiguous; counts matched the recorded trial. Returning login
  retained progress and skipped completed consent/training. Refresh/relogin did
  not duplicate the original session or its research records.
- Direct end-user requests confirmed that another account cannot read the test
  player's profile and that a valid-shaped research start record is rejected
  when the current saved consent is declined (HTTP 403).
- A separate real signup used the Firebase Auth REST API. The owner received and
  opened its confirmation email; Firebase reported verified, and login through
  the hosted game opened first-time consent. This does not claim a repeat of the
  Create account UI submission test. An abandoned consent page did not mark
  training complete. Reload returned to Log in; active gameplay is not resumed.
- No new browser console errors were recorded in the corrected release. All
  88 packaged AAC recordings still matched the measured browser profile, and
  all 603 non-Editor assets checked against the pre-build baseline were unchanged.
  The build had zero errors and two existing URP shader truncation warnings.
- Both disposable accounts and their 22 test documents were removed after
  verification. No existing player records, rules, billing or main-site release
  were changed.

Remaining release checks: a human listening/control test on another computer,
and Firefox/Safari coverage if those browsers will be supported. Hosted behavior
was exercised in Chromium; this is not a new perceptual loudness study. Review
the preview before promoting the exact tested version with the clone command
above. No further build is needed unless project files change.

Free-plan quotas still apply. Per-note documents use one write each; monitor
actual trial volume before a public launch. No public leaderboard is exposed.

## Deliberately deferred

- Existing Supabase records are deliberately not imported (owner's fresh-start choice).
- Leaderboard UI, nickname editing, history browsing, researcher exports.
- Automatic account/data deletion. Rules cannot run background cleanup; an
  administrator procedure or a narrowly scoped function can be added later.

Pending checkpoints and research requests now have a per-project, per-account
local outbox. Login to the same account retries it before another run. This
does not resume gameplay. Storage failure, clearing browser data, or closing
before browser storage finishes can still lose unsent records; a trial-start
marker distinguishes incomplete observations. The 24-hour research upload window
and current-consent-receipt checks still apply.

Official references: [Auth REST](https://firebase.google.com/docs/reference/rest/auth),
[Unity SDK platform support](https://firebase.google.com/docs/unity/learn-more),
[Firestore rules](https://firebase.google.com/docs/firestore/security/get-started),
[Hosting headers](https://firebase.google.com/docs/hosting/full-config).
