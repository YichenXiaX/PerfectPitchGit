# Comet Catcher backend

The game now uses **Firebase Authentication and Cloud Firestore**, with Firebase
Hosting configuration for the Unity web build. There are no Cloud Functions,
Extensions or separate authentication systems. Billing is not enabled.

Project: `comet-catcher`. Firestore: `(default)`, Standard/Native, Iowa
(`us-central1`). Email/password is enabled; passwordless login and Analytics are
not used. Firebase's hosted confirmation page handles verification links.

The existing account, consent, tutorial and game screens are reused. Only a
verified account may access its own records. Consent is saved before training;
training completion is saved per account and version. Everyone can save scores
and progress. Only consenting players can save detailed normal-game research
records. Practice is excluded. Retries reuse the same document IDs.

See [Firebase setup, schema, tests and release status](firebase/README.md).

The owner chose a fresh start on 2026-11-07. Existing Supabase accounts and data
remain untouched in the old hosted project; they are not imported into Firebase.
Players must create a Firebase account, including those who used the earlier
Supabase build. The replaced client code, SQL migrations, Edge Functions and
obsolete provider tests have been removed. A source snapshot outside the game
project preserves uncommitted work from before cleanup.

Verification passed 13 database-rule tests and 31 Unity integration checks.
Real verification email delivery, live consent/returning-login checks, and a
normal Editor run with its completed progress and research save also passed.

The schema-2 research update subsequently passed 18 rule tests, 35 research
checks in running Play mode, the 31 existing account checks, and live Firebase
save/ownership/consent checks. Its rules are deployed. See the
[research data dictionary and timing limits](firebase/RESEARCH-DATA.md).

The earlier WebGL calibration mismatch and missing comet-effect shader have
been fixed. The measured browser calibration and a complete local browser
tutorial/gameplay/replay journey passed; that journey used local emulators.
On 2026-11-09 a non-development release also passed the hosted Chromium journey
against real Firebase: consent, the full tutorial, gameplay, replay, cloud saves,
logout and returning login. A consenting run saved one trial and 14 notes;
two declined runs saved progress with no research records. A new signup through
the Auth API received a real verification email, the owner confirmed it, and
its browser login correctly opened consent. Test accounts and their records
were removed afterward. See the [Hosting instructions](firebase/README.md#hosting)
for the temporary preview, verification limits and promotion of the tested version.
The main/live site has not been published.
