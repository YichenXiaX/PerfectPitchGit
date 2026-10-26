# Comet Catcher backend

TypeScript on Supabase Edge Functions, PostgreSQL storage, and Supabase Auth for accounts. Unity remains C#. Deploy this folder separately from the Unity WebGL build.

## Implemented

- Verified email accounts required; anonymous accounts rejected.
- Private profiles, unique nicknames, cumulative session summaries, progress and history.
- Ownership checks, idempotent retries, ordered checkpoints and immutable completed sessions.
- Opt-in leaderboard for completed standard games, separated by game version.
- Account deletion with cascading removal of game records.
- Strict payload validation in both HTTP and database layers, including direct RPC calls.

Research collection is disabled for everyone in this portfolio version. No consent documents, names, contact preferences, individual responses or reaction-time arrays are accepted. Supabase Auth holds account email addresses; the leaderboard publishes only nicknames, ranks and scores.

Scores are client-reported. Validation cannot prove honest play or settings. Do not treat the leaderboard as validated research evidence.

## Unity connection

Signup, email verification, login/logout, progress display and summary saving are now integrated. See [Unity integration and testing](UNITY-INTEGRATION.md). A real-account test is still needed. Password recovery, leaderboard preferences and account deletion UI are not included yet.

## Your setup

1. Create an account at [Supabase](https://supabase.com/dashboard) and a project named Comet Catcher (or your preferred name).
2. Save the database password privately. Never put database passwords, service-role keys, SMTP passwords or personal access tokens in chat/source control.
3. Record the project reference, project URL and publishable key. The URL and publishable key can be used by the browser.
4. Tell us when the project is ready so we can link and deploy to that project.

No domain or website host is needed yet. Before public registration, configure custom SMTP: Supabase's built-in mail service is for testing and restricts recipients to project team addresses. See [SMTP documentation](https://supabase.com/docs/guides/auth/auth-smtp).

In hosted Auth settings: enable email/password signup and email confirmation, disable anonymous sign-in, require passwords of at least 12 characters, and configure the actual site/callback/recovery URLs once those pages exist. The local config.toml Auth settings are NOT automatically applied to hosted Auth by migration/function deployment.

## Local checks

Install Node.js 24+ with npm. From this Backend folder:

```powershell
npm ci
npm run check
```

Tests execute the real SQL migration in PGlite (PostgreSQL WebAssembly), with a small test Auth schema. HTTP tests mock remote services. TypeScript checks include the entrypoint with a minimal Deno type declaration. These checks do not verify hosted Auth, Edge deployment, or Unity WebGL.

For a full local Supabase stack, install a supported Docker-compatible runtime and run npx supabase start. Docker is not required for the included tests. See [local development](https://supabase.com/docs/guides/local-development/cli/getting-started).

## Deployment after project creation

From this folder:

```powershell
npx supabase login
npx supabase link --project-ref YOUR_PROJECT_REFERENCE
npx supabase db push --dry-run
npx supabase db push
npx supabase secrets set ALLOWED_ORIGINS=https://YOUR_GAME_WEBSITE
npx supabase functions deploy game-api
```

Complete login privately. Review the migration preview before applying, especially on an existing project. Origins must be exact scheme/host/port, with no path/trailing slash; separate multiple origins with commas. Keep JWT verification enabled. The handler additionally verifies each token with Auth.

Hosted functions receive SUPABASE_URL, SUPABASE_ANON_KEY and SUPABASE_SERVICE_ROLE_KEY automatically. Optional GAME_PUBLISHABLE_KEY overrides the anon key for normal requests. The service-role key is used only on the server for account deletion; never embed it in Unity or the website. Database requests use the caller's token.

The CLI dependency is pinned. Running it in the agent sandbox failed because it could not create its home configuration directory. CLI login, configuration validation and deployment remain to be verified in your terminal. No cloud changes have been made.

## API

Base URL: https://YOUR_PROJECT.supabase.co/functions/v1/game-api

Send Authorization: Bearer USER_ACCESS_TOKEN and the public apikey header. Bodies require Content-Type: application/json. Every route requires a verified account.

| Method/path | Exact fields or query parameters |
| --- | --- |
| GET /profile | None |
| PUT /profile | nickname, leaderboardEnabled |
| POST /sessions | sessionId, gameVersion, mode |
| PUT /checkpoints | sessionId, revision, score, highestLevel, elapsedSeconds, completed |
| GET /progress | None |
| GET /history?limit=20&offset=0 | Both limit (1–50) and offset required |
| GET /leaderboard?gameVersion=0.1.0 | gameVersion required |
| DELETE /account | confirmation must equal DELETE |

Extra fields are rejected; bodies are limited to 4096 bytes. Nicknames use 3–24 ASCII letters/numbers/underscores/hyphens starting with a letter, unique ignoring case. Leaderboard visibility defaults off. Profiles return researchCollectionEnabled: false.

Start body:
```json
{"sessionId":"4a6c047e-ff6a-4ac8-9c3a-07582e376d46","gameVersion":"0.1.0","mode":"standard"}
```

Checkpoint body:
```json
{"sessionId":"4a6c047e-ff6a-4ac8-9c3a-07582e376d46","revision":1,"score":1200,"highestLevel":0,"elapsedSeconds":30,"completed":false}
```

Errors contain an error code: 401 invalid/expired login, 403 unauthorized, 400 bad input, 409 save/nickname conflict, 413 oversized body, 429 rate limit (Retry-After), 503 provider unavailable. Raw provider errors are not exposed.

## Unity integration contract

- Require verified login before starting a server session. Never use a caller-supplied user ID as authorization.
- Generate one UUID per run and start it before play. Retry with the same UUID, mode and version after network failure.
- Save cumulative summaries about every 20–30 seconds and at game end. Round gameManager.aura to a signed integer; the existing session DTO lacks that score. Current level indices are 0–3; update API and SQL together if expanding levels.
- Increment revision for new checkpoints; retry uncertain saves with identical revision and payload. Elapsed time and highest level cannot decrease. Fetch history to reconcile conflicts rather than overwriting newer saves.
- Completed sessions are immutable except identical retries. Replay starts a new UUID. Clear queued saves on account switch/logout.
- Saves expire after 24 hours; time checks allow five seconds beyond server elapsed time. Limits: 20 new sessions/hour and 120 successful database requests/minute/account.
- Custom settings use custom mode; only completed standard sessions enter leaderboards. Increment gameVersion when scoring changes.
- Add explicit account-deletion confirmation UI. Live records cascade-delete; backup retention is a separate provider policy.

Before public release, test real signup/verification/recovery/logout, two-account isolation, save retries, opt-in/out, deletion, browser CORS and WebGL. Configure mail delivery, signup abuse protection, budget alerts and backup/retention policies. Research activation requires separate consent enforcement and a reviewed data model; there is no research-enable switch here.

## Files

- supabase/migrations/202610250001_portfolio_backend.sql — database and access rules.
- supabase/functions/game-api/handler.ts — HTTP API.
- supabase/functions/game-api/index.ts — Edge entrypoint.
- supabase/config.toml — local stack/function configuration.
- tests/ — database/API checks and local Deno type declaration.
- .env.example — origin configuration example, no secrets.
- package.json, package-lock.json, tsconfig.json — dependencies and checks.
