# Unity accounts and cloud progress

The public build now starts at Assets/Scenes/account.unity. Players create an email/password account, verify the email, log in and view saved best score, highest level and run counts. Play opens the existing game. Each run starts fresh; saved progress does not restore an unfinished round.

## First real-account test

1. Supabase → Authentication → Email Templates → Confirm signup: add a line displaying {{ .Token }} in the email body. This lets players enter their verification code inside Unity. You may keep the existing confirmation link.
2. While using the default mail service, use your Supabase organization team email address. Public signup needs custom SMTP: https://supabase.com/docs/guides/auth/auth-smtp
3. Open the account scene and press Play. Enter email and a password of at least 12 characters, then Create account.
4. Read the email privately, enter the code, and select Verify email.
5. Play a run, wait for Progress saved, then Save & log out.
6. Log back in and check that the saved scores and run counts remain. Test a second account to confirm isolation.

Your Supabase dashboard account is separate from the in-game player account; create the latter through the game. Do not share passwords, tokens or verification codes.

Hosted Auth settings were checked: email signup enabled, confirmation required, anonymous users disabled. Live authenticated saving still needs the real-account test above.

## Saving and sessions

UnityWebRequest talks directly to Supabase Auth and the game API over HTTPS. No JavaScript bridge is necessary for this email/password/code flow. Passwords and tokens stay in memory; closing the game requires login again. Tokens refresh automatically during a session.

The game waits for the server to create a session. Summary checkpoints are sent every 20 seconds, at game over and before logout. Failed requests retain identical payloads and revisions for retry. Replay waits for the previous final save. Account pauses play/audio; Return to game resumes the prior state. Save & log out flushes pending progress before clearing identity and returning to login.

The bottom bar shows cloud status. The Account menu offers Retry cloud save when necessary. Offline logout stays blocked while progress is unsaved. Keep the browser open until Progress saved: a browser close cannot reliably finish an HTTP request, and the in-memory queue does not survive closing. Only the last acknowledged checkpoint is durable.

Only score, level, elapsed time and session metadata are sent. Full trial JSON is no longer written by dataManager. Historical local files are untouched. Detailed gameManager/frequencyManager logs are editor-only. Trial objects remain temporarily in memory for game-over statistics. Research collection is disabled; the consent demo is preserved but excluded from the portfolio build.

## WebGL and remaining work

The deployed API currently allows http://localhost:5173. Serve local WebGL testing at that exact origin. Add the real HTTPS website origin to ALLOWED_ORIGINS before publishing. Configure Auth website/redirect URLs too. Do not open WebGL through file://.

Password recovery, leaderboard preferences and account deletion UI are not included in this increment. Current backend bounds are 24-hour sessions and level indices 0–3. Scores remain client-reported.

## Verification and files

Editor checks use a fake transport excluded from player builds. They cover unverified accounts, progress loading, start/save retries, offline retention, paused final saves, token refresh and logout cleanup. Actual SampleScene checks cover cloud start, session IDs, pause/resume, periodic/final summaries, replay and logout.

To rerun in a clean Play session through Unity script eval, call cloudIntegrationChecks.run() or cloudIntegrationChecks.runGameplay(), then read cloudIntegrationChecks.result. Stop Play afterwards; checks use fake accounts and change scenes. They do not replace hosted account/browser testing.

- Assets/Scripts/Accounts/cloudAccount.cs: Auth, token refresh and cloud save queue.
- Assets/Scripts/Accounts/accountUi.cs: UI callbacks.
- Assets/Resources/accountUi.prefab: dark account interface.
- Assets/Scenes/account.unity: startup scene.
- Assets/Editor/Accounts/accountUiBuilder.cs: scene/prefab builder.
- Assets/Editor/Accounts/cloudIntegrationChecks.cs: editor-only integration checks.
- gameManager.cs: server session start gate and start event.
- dataManager.cs: summary-only game-over save.
- gameOverUi.cs: asynchronous replay UI.
- settingsUi.cs: retain start overlay until cloud session succeeds.
- playerController.cs: no gameplay input while paused.
- frequencyManager.cs: detailed diagnostics excluded from public players.

## Verification result for this integration

- 12 service checks passed with the editor-only mock transport.
- 8 real-scene integration checks passed with the same mock server approach.
- WebGL player scripts compiled successfully (31 assemblies).
- Hosted browser preflight returned 204 for http://localhost:5173.
- Hosted API returned 401 without authentication.
- Login UI was visually inspected in Play mode; one EventSystem was present.
- Full WebGL packaging was canceled at Unity's prompt to save the already-modified researchConsent scene. Those unsaved scene changes were left untouched. A complete build/browser test remains outstanding.
- No real player account was created or used by these checks. The email signup and authenticated hosted save test must still be completed by you.
