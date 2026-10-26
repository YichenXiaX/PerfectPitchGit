import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { PGlite } from "@electric-sql/pglite";
const db = new PGlite();
const alice = "11111111-1111-4111-8111-111111111111";
const bob = "22222222-2222-4222-8222-222222222222";
const guest = "33333333-3333-4333-8333-333333333333";
const unverified = "44444444-4444-4444-8444-444444444444";
const session = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
before(async () => {
  await db.exec("create role anon; create role authenticated; create schema auth; create table auth.users(id uuid primary key,email text,email_confirmed_at timestamptz,is_anonymous boolean default false); create function auth.uid() returns uuid language sql as $$select nullif(current_setting('request.jwt.claim.sub',true),'')::uuid$$; grant usage on schema public,auth to authenticated,anon; grant execute on function auth.uid() to authenticated,anon;");
  await db.query("insert into auth.users values ($1,'a@example.invalid',now(),false),($2,'b@example.invalid',now(),false),($3,'g@example.invalid',now(),true),($4,'u@example.invalid',null,false)", [alice,bob,guest,unverified]);
  await db.exec(await readFile(new URL("../supabase/migrations/202610250001_portfolio_backend.sql", import.meta.url), "utf8"));
});
after(async () => { await db.close(); });
async function asUser<T>(user: string, work: () => Promise<T>) {
  await db.query("select set_config('request.jwt.claim.sub',$1,false)",[user]);
  await db.exec("set role authenticated");
  try { return await work(); } finally { await db.exec("reset role"); }
}
async function api(user: string, action: string, payload: unknown = {}) {
  return asUser(user, async () => {
    const result = await db.query<{ value: any }>("select public.game_api($1,$2::jsonb) as value",[action,JSON.stringify(payload)]);
    return result.rows[0].value;
  });
}
const checkpoint = (overrides = {}) => ({sessionId:session,revision:1,score:1234,highestLevel:1,elapsedSeconds:0,completed:false,...overrides});
test("anonymous and unconfirmed identities cannot use the database API", async () => {
  for(const user of ["",guest,unverified]) await assert.rejects(api(user,"getProfile"),/verified account/);
  await db.exec("set role anon");
  try { await assert.rejects(db.query("select public.game_api('getProfile','{}')"),/permission denied/); }
  finally { await db.exec("reset role"); }
});
test("private tables cannot be read or written directly", async () => {
  await asUser(alice, async () => {
    await assert.rejects(db.query("select * from game_private.profiles"),/permission denied/);
    await assert.rejects(db.query("insert into game_private.sessions(session_id,user_id,game_version,mode) values($1,$2,'1','standard')",[session,bob]),/permission denied/);
    await assert.rejects(db.query("select game_private.expect_keys('{}','{}')"),/permission denied/);
  });
});
test("profiles default to private nicknames and reject PII and research fields", async () => {
  const profile = await api(alice,"getProfile");
  assert.match(profile.nickname,/^player_/);assert.equal(profile.leaderboardEnabled,false);
  assert.equal(profile.researchCollectionEnabled,false);
  await api(alice,"updateProfile",{nickname:"Alice_Player",leaderboardEnabled:true});
  await api(bob,"updateProfile",{nickname:"Bob_Player",leaderboardEnabled:false});
  await assert.rejects(api(bob,"updateProfile",{nickname:"alice_player",leaderboardEnabled:true}),/unique constraint/);
  await assert.rejects(api(alice,"updateProfile",{nickname:"a@example.com",leaderboardEnabled:false}),/Invalid profile/);
  await assert.rejects(api(alice,"updateProfile",{nickname:"Alice_Player",leaderboardEnabled:false,email:"x"}),/fields/);
  await assert.rejects(api(alice,"recordConsent",{adultAgreed:true}),/research collection is disabled/);
});
test("session start is idempotent and enforces ownership", async () => {
  const payload={sessionId:session,gameVersion:"portfolio-1",mode:"standard"};
  const first=await api(alice,"startSession",payload);
  assert.deepEqual(await api(alice,"startSession",payload),first);
  await assert.rejects(api(bob,"startSession",payload),/SESSION_CONFLICT/);
  await assert.rejects(api(alice,"startSession",{...payload,gameVersion:"different"}),/SESSION_CONFLICT/);
  assert.equal((await api(bob,"getHistory",{limit:20,offset:0})).sessions.length,0);
});
test("direct RPC rejects detailed Unity payloads and invalid numbers", async () => {
  for(const extra of [{results:[{predictionTime:1}]},{audioLoudness:{}},{researchConsentStatus:"adultAgreed"},{contact:"test"},{userId:bob}])
    await assert.rejects(api(alice,"saveCheckpoint",{...checkpoint(),...extra}),/fields/);
  await assert.rejects(api(alice,"saveCheckpoint",checkpoint({score:null})),/integers/);
  await assert.rejects(api(alice,"saveCheckpoint",checkpoint({score:1.5})),/integers/);
  await assert.rejects(api(alice,"saveCheckpoint",checkpoint({score:1000000001})),/limits/);
  await assert.rejects(api(alice,"saveCheckpoint",checkpoint({highestLevel:4})),/limits/);
  await assert.rejects(api(alice,"saveCheckpoint",checkpoint({elapsedSeconds:100})),/duration/);
});
test("checkpoint retries, ordering, completion and private history", async () => {
  await assert.rejects(api(bob,"saveCheckpoint",checkpoint()),/SESSION_NOT_FOUND/);
  const first=await api(alice,"saveCheckpoint",checkpoint());
  assert.deepEqual(await api(alice,"saveCheckpoint",checkpoint()),first);
  await assert.rejects(api(alice,"saveCheckpoint",checkpoint({score:999})),/CHECKPOINT_CONFLICT/);
  const final=await api(alice,"saveCheckpoint",checkpoint({revision:2,score:1500,completed:true}));
  assert.equal(final.completed,true);assert.ok(final.completedAt);
  assert.deepEqual(await api(alice,"saveCheckpoint",checkpoint({revision:2,score:1500,completed:true})),final);
  await assert.rejects(api(alice,"saveCheckpoint",checkpoint({revision:3,score:1600})),/CHECKPOINT_CONFLICT/);
  const history=await api(alice,"getHistory",{limit:10,offset:0});
  assert.equal(history.sessions.length,1);assert.equal(history.sessions[0].score,1500);
  assert.equal(history.sessions[0].userId,undefined);
  const progress=await api(alice,"getProgress");
  assert.equal(progress.sessionsPlayed,1);assert.equal(progress.bestScore,1500);
});
test("leaderboard opt-out, version separation and custom-play exclusion", async () => {
  const board=await api(bob,"getLeaderboard",{gameVersion:"portfolio-1"});
  assert.deepEqual(board.entries,[{rank:1,nickname:"Alice_Player",score:1500}]);
  assert.equal(board.verification,"clientReported");
  assert.deepEqual((await api(bob,"getLeaderboard",{gameVersion:"other"})).entries,[]);
  await api(alice,"updateProfile",{nickname:"Alice_Player",leaderboardEnabled:false});
  assert.deepEqual((await api(bob,"getLeaderboard",{gameVersion:"portfolio-1"})).entries,[]);
  const custom="bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
  await api(bob,"updateProfile",{nickname:"Bob_Player",leaderboardEnabled:true});
  await api(bob,"startSession",{sessionId:custom,gameVersion:"portfolio-1",mode:"custom"});
  await api(bob,"saveCheckpoint",checkpoint({sessionId:custom,completed:true,score:9999}));
  assert.deepEqual((await api(bob,"getLeaderboard",{gameVersion:"portfolio-1"})).entries,[]);
});
test("authenticated request limits are enforced", async () => {
  await db.query("update game_private.request_limits set request_count=120,window_started_at=now() where user_id=$1",[alice]);
  await assert.rejects(api(alice,"getProfile"),/RATE_LIMIT/);
  await db.query("update game_private.request_limits set window_started_at=now()-interval '2 minutes' where user_id=$1",[alice]);
  assert.equal((await api(alice,"getProfile")).nickname,"Alice_Player");
});
test("deleting an account removes its private records", async () => {
  await db.query("delete from auth.users where id=$1",[alice]);
  for(const table of ["profiles","sessions","request_limits"]) {
    const rows=await db.query<{count: number}>("select count(*)::integer as count from game_private."+table+" where user_id=$1",[alice]);
    assert.equal(rows.rows[0].count,0);
  }
  await assert.rejects(api(alice,"getProfile"),/verified account/);
});
