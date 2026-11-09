import { test } from 'node:test';
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';

// Use the same REST endpoints as the Unity Editor and WebGL client. No Admin SDK
// participates in database tests: every database request uses an end-user token.
const project = 'demo-comet-catcher';
const authHost = process.env.FIREBASE_AUTH_EMULATOR_HOST;
const dbHost = process.env.FIRESTORE_EMULATOR_HOST;
assert.ok(authHost?.startsWith('127.0.0.1:') && dbHost?.startsWith('127.0.0.1:'), 'Tests run only against local emulators');
const root = `projects/${project}/databases/(default)/documents`;
const db = `http://${dbHost}/v1/${root}`;
const auth = `http://${authHost}/identitytoolkit.googleapis.com/v1`;
const stamp = Symbol('server timestamp');
function encode(v) {
  if (v === null) return { nullValue: null };
  if (Array.isArray(v)) return {arrayValue:{values:v.map(encode)}};
  if (typeof v === 'boolean') return { booleanValue: v };
  if (typeof v === 'number') return Number.isInteger(v) ? { integerValue: String(v) } : { doubleValue: v };
  if (typeof v === 'string') return { stringValue: v };
  return { mapValue: { fields: Object.fromEntries(Object.entries(v).map(([k,x]) => [k,encode(x)])) } };
}
function decode(v) {
  if ('arrayValue' in v) return (v.arrayValue.values??[]).map(decode);
  if ('stringValue' in v) return v.stringValue;
  if ('integerValue' in v) return Number(v.integerValue);
  if ('doubleValue' in v) return v.doubleValue;
  if ('booleanValue' in v) return v.booleanValue;
  if ('timestampValue' in v) return v.timestampValue;
  if ('mapValue' in v) return Object.fromEntries(Object.entries(v.mapValue.fields ?? {}).map(([k,x]) => [k,decode(x)]));
  return null;
}
async function call(url, token, body, method = 'POST') {
  const response = await fetch(url, { method, headers: { 'Content-Type':'application/json', ...(token ? { Authorization:`Bearer ${token}` } : {}) }, body: body === undefined ? undefined : JSON.stringify(body) });
  return { status:response.status, body:await response.json() };
}
function write(path, fields, exists) {
  const values = Object.fromEntries(Object.entries(fields).filter(([,v]) => v !== stamp).map(([k,v]) => [k,encode(v)]));
  const transforms = Object.entries(fields).filter(([,v]) => v === stamp).map(([fieldPath]) => ({ fieldPath, setToServerValue:'REQUEST_TIME' }));
  return { update:{ name:`${root}/${path}`, fields:values }, ...(transforms.length ? { updateTransforms:transforms } : {}), ...(exists === undefined ? {} : { currentDocument:{ exists } }) };
}
async function commit(user, writes) { return call(db + ':commit', user?.token, {writes}); }
async function read(user, path) { return call(`${db}/${path}`, user?.token, undefined, 'GET'); }
function document(result) { return Object.fromEntries(Object.entries(result.body.fields ?? {}).map(([k,v]) => [k,decode(v)])); }
function allowed(result) { assert.equal(result.status,200, JSON.stringify(result.body)); }
function denied(result) { assert.equal(result.status,403, JSON.stringify(result.body)); }
function freshProfile() { return { schemaVersion:1,nickname:'player_test',consentReceiptId:'',tutorialVersion:0,tutorialCompletedAt:null,createdAt:stamp,updatedAt:stamp }; }
async function user(verified=true) {
  const email = `${randomUUID()}@example.test`, password = 'Test-password-123!';
  const created = await call(`${auth}/accounts:signUp?key=test-key`, null, { email,password,returnSecureToken:true });
  allowed(created);
  if (verified) allowed(await call(`${auth}/accounts:update?key=test-key`, 'owner', { localId:created.body.localId,emailVerified:true }));
  const signed = await call(`${auth}/accounts:signInWithPassword?key=test-key`, null, { email,password,returnSecureToken:true });
  allowed(signed);
  return { uid:signed.body.localId, token:signed.body.idToken, email,password, path:`players/${signed.body.localId}` };
}
function consent(decision='adultAgreed') {
  return { schemaVersion:1,requestId:randomUUID(),formVersion:'2026-10-25-v1',decision,adultAgreement:decision==='adultAgreed',childAgreement:decision==='childAndParentAgreed',parentAgreement:decision==='childAndParentAgreed',participantName:'',childName:'',parentChildName:'',guardianName:'',participantFollowUp:'notSpecified',guardianFollowUp:'notSpecified',participantContact:'',guardianContact:'',savedAt:stamp };
}
async function saveConsent(u, c=consent()) {
  const result = await read(u,u.path); allowed(result);
  // Patch only the pointer; preserve typed timestamps on all other fields.
  const profileWrite = { update:{name:`${root}/${u.path}`,fields:{consentReceiptId:encode(c.requestId)}}, updateMask:{fieldPaths:['consentReceiptId']}, updateTransforms:[{fieldPath:'updatedAt',setToServerValue:'REQUEST_TIME'}], currentDocument:{updateTime:result.body.updateTime} };
  return commit(u,[write(`${u.path}/consents/${c.requestId}`,c,false),profileWrite]);
}
async function train(u) {
  return commit(u,[{ update:{name:`${root}/${u.path}`,fields:{tutorialVersion:encode(1)}}, updateMask:{fieldPaths:['tutorialVersion']}, updateTransforms:[{fieldPath:'tutorialCompletedAt',setToServerValue:'REQUEST_TIME'},{fieldPath:'updatedAt',setToServerValue:'REQUEST_TIME'}] }]);
}
async function prepared(decision='adultAgreed') {
  const u=await user(); allowed(await commit(u,[write(u.path,freshProfile(),false)]));
  const c=consent(decision); allowed(await saveConsent(u,c)); allowed(await train(u));
  u.receipt=c; return u;
}
function session(c) { return {schemaVersion:1,gameVersion:'0.1.0',mode:'standard',consentReceiptId:c.requestId,revision:0,score:0,highestLevel:0,elapsedSeconds:0,completed:false,createdAt:stamp,updatedAt:stamp}; }
async function start(u) { const path=`${u.path}/sessions/${randomUUID()}`; allowed(await commit(u,[write(path,session(u.receipt),false)])); return path; }
function trial(count=1) { return {schemaVersion:1,roundIndex:0,correctQuadrant:1,predictedQuadrant:0,secondGuessQuadrant:1,level:0,predictionTime:0.3,resolvedTime:1,outcome:'Destroyed',noteCount:count,savedAt:stamp,audioLoudness:{profileId:'profile',model:'model',playbackCalibration:'relative',pressurePaPerDigitalUnit:0,noteDurationSeconds:0.4,fadeDurationSeconds:0.02,masterVolume:0.7,plannedTargetPeakSones:1}}; }
function note(index=0) { return {schemaVersion:1,index,clipName:'C4',requestedFrequencyHz:392.15,nominalPlaybackFrequencyHz:261.63,pitchRatio:1.499,playbackStartFrame:0,playbackSampleRate:44100,sourceVolume:0.3,plannedPeakSones:1,plannedDurationSeconds:0.4,elapsedSecondsAtResult:0.4,resultBeforePlannedEnd:false,requestedTimeInSequenceSeconds:0}; }
function trialWrites(s,count=1) { return [write(`${s}/trials/0`,trial(count),false), ...Array.from({length:count},(_,i)=>write(`${s}/trials/0/notes/${i}`,note(i),false))]; }

test('unverified accounts and anonymous requests cannot read or create profiles',async()=>{
  const u=await user(false); denied(await commit(u,[write(u.path,freshProfile(),false)])); denied(await read(u,u.path)); denied(await read(null,u.path));
});
test('verified owner can create profile; another player cannot read or overwrite it',async()=>{
  const a=await user(), b=await user(); allowed(await commit(a,[write(a.path,freshProfile(),false)]));
  allowed(await read(a,a.path)); denied(await read(b,a.path)); denied(await commit(b,[write(a.path,freshProfile())]));
  denied(await call(db+'/players',a.token,undefined,'GET'));
});
test('profile cannot forge consent, mark tutorial complete on creation, or add permission flags',async()=>{
  const u=await user();
  denied(await commit(u,[write(u.path,{...freshProfile(),tutorialVersion:1},false)]));
  denied(await commit(u,[write(u.path,{...freshProfile(),researchAllowed:true},false)]));
  allowed(await commit(u,[write(u.path,freshProfile(),false)]));
  denied(await train(u));
  denied(await commit(u,[{update:{name:`${root}/${u.path}`,fields:{consentReceiptId:encode(randomUUID())}},updateMask:{fieldPaths:['consentReceiptId']},updateTransforms:[{fieldPath:'updatedAt',setToServerValue:'REQUEST_TIME'}]}]));
});
test('adult consent is atomic and immutable; retries find one confirmed receipt',async()=>{
  const u=await prepared(); const path=`${u.path}/consents/${u.receipt.requestId}`;
  const result=await read(u,path); allowed(result); assert.equal(document(result).decision,'adultAgreed');
  const duplicate=await saveConsent(u,u.receipt); assert.ok([403,409].includes(duplicate.status));
  allowed(await read(u,path)); const listing=await call(`${db}/${u.path}/consents`,u.token,undefined,'GET'); allowed(listing); assert.equal(listing.body.documents.length,1);
  denied(await commit(u,[write(path,{...u.receipt,participantName:'Changed'})]));
});
test('child consent requires guardian agreement and both follow-up permissions for contact',async()=>{
  const u=await user(); allowed(await commit(u,[write(u.path,freshProfile(),false)]));
  denied(await saveConsent(u,{...consent('childAndParentAgreed'),parentAgreement:false}));
  denied(await saveConsent(u,{...consent('childAndParentAgreed'),participantFollowUp:'yes',participantContact:'test@example.test'}));
  allowed(await saveConsent(u,consent('childAndParentAgreed')));
});
test('decline cannot carry names, agreement flags, contacts, old form versions, or extra fields',async()=>{
  const u=await user(); allowed(await commit(u,[write(u.path,freshProfile(),false)]));
  for(const fields of [{participantName:'Person'},{adultAgreement:true},{participantContact:'mail@example.test'},{formVersion:'old'},{extra:'unexpected'}]) denied(await saveConsent(u,{...consent('declined'),...fields}));
  allowed(await saveConsent(u,consent('declined')));
});
test('saving a run requires consent and training; tutorial sessions are not accepted',async()=>{
  const u=await user(); allowed(await commit(u,[write(u.path,freshProfile(),false)])); const c=consent();
  const path=`${u.path}/sessions/${randomUUID()}`;
  denied(await commit(u,[write(path,session(c),false)])); allowed(await saveConsent(u,c)); denied(await commit(u,[write(path,session(c),false)]));
  allowed(await train(u)); denied(await commit(u,[write(path,{...session(c),mode:'tutorial'},false)])); allowed(await commit(u,[write(path,session(c),false)]));
});
test('declining player can save scores but cannot upload research',async()=>{
  const u=await prepared('declined'),s=await start(u); denied(await commit(u,trialWrites(s)));
  allowed(await commit(u,[{update:{name:`${root}/${s}`,fields:{revision:encode(1),score:encode(140),highestLevel:encode(1),elapsedSeconds:encode(10),completed:encode(true)}},updateMask:{fieldPaths:['revision','score','highestLevel','elapsedSeconds','completed']},updateTransforms:[{fieldPath:'updatedAt',setToServerValue:'REQUEST_TIME'}]}]));
  assert.equal(document(await read(u,s)).score,140);
});
test('checkpoints reject stale revisions, extra data, and mutation after completion',async()=>{
  const u=await prepared(),s=await start(u);
  const patch=(fields)=>({update:{name:`${root}/${s}`,fields:Object.fromEntries(Object.entries(fields).map(([k,v])=>[k,encode(v)]))},updateMask:{fieldPaths:Object.keys(fields)},updateTransforms:[{fieldPath:'updatedAt',setToServerValue:'REQUEST_TIME'}]});
  denied(await commit(u,[patch({revision:2})])); denied(await commit(u,[patch({revision:1,email:'person@example.test'})]));
  allowed(await commit(u,[patch({revision:1,score:300,elapsedSeconds:2,completed:true})]));
  denied(await commit(u,[patch({revision:2,score:400})])); assert.equal(document(await read(u,s)).revision,1);
});
test('consenting player can atomically save a full 64-note trial with exact pitch details',async()=>{
  const u=await prepared(),s=await start(u); allowed(await commit(u,trialWrites(s,64)));
  const actual=document(await read(u,`${s}/trials/0/notes/0`)); assert.equal(actual.requestedFrequencyHz,392.15);
  denied(await commit(u,[write(`${s}/trials/0/notes/0`,note())]));
  const other=await user(); denied(await read(other,`${s}/trials/0`)); denied(await commit(other,trialWrites(s)));
});
test('invalid note prevents the entire trial from saving; nested identifying data is rejected',async()=>{
  const u=await prepared(),s=await start(u); const writes=trialWrites(s); writes[1]=write(`${s}/trials/0/notes/0`,{...note(),email:'person@example.test'},false);
  denied(await commit(u,writes)); assert.equal((await read(u,`${s}/trials/0`)).status,404);
  for(const invalid of [{pitchRatio:-1},{sourceVolume:2},{requestedFrequencyHz:'bad'},{index:64}]) denied(await commit(u,[write(`${s}/trials/0`,trial(),false),write(`${s}/trials/0/notes/0`,{...note(),...invalid},false)]));
});
test('changing consent immediately blocks queued research from an old session and cannot reactivate an old receipt',async()=>{
  const u=await prepared(),s=await start(u); const old=u.receipt;
  allowed(await saveConsent(u,consent('declined'))); denied(await commit(u,trialWrites(s)));
  allowed(await saveConsent(u,consent())); denied(await commit(u,trialWrites(s)));
  assert.ok([403,409].includes((await saveConsent(u,old)).status));
});
test('direct deletes of consent, profile, or sessions are denied',async()=>{
  const u=await prepared(),s=await start(u);
  for(const path of [u.path,`${u.path}/consents/${u.receipt.requestId}`,s]) denied(await call(`${db}/${path}`,u.token,undefined,'DELETE'));
});

function startedTrial() {
  return {schemaVersion:2,roundIndex:0,correctQuadrant:1,level:0,state:'started',startedAtUnixMs:Date.now(),octaveShift:null,
    settings:{mode:'standard',controlLayout:'right',notesPerSequence:2,consecutiveCorrectToAdvance:5,allowedOctaveShifts:[-1,0,1],
      noteDuration:0.4,pauseBetweenNotes:0.2,predictionWindow:1,cometSpawnDelay:0.5,pauseBetweenRounds:1,cometSpeed:3,shotInterval:0.2},
    firstDecision:null,secondDecision:null,cometSpawnedAtMs:null,resolvedAtMs:null,predictionStatus:'notAttempted',resolution:null,
    interruptionReason:null,wasPaused:false,lostFocus:false,settingsChanged:false,audioLoudness:null,noteCount:0,savedAt:stamp};
}
function resultTrial(t) {
  return {...t,state:'resolved',octaveShift:-1,firstDecision:{quadrant:0,atMs:200,mostRecentToneIndex:0,activeToneIndex:0},
    secondDecision:{quadrant:1,atMs:350,mostRecentToneIndex:0,activeToneIndex:0},cometSpawnedAtMs:700,resolvedAtMs:1000,
    predictionStatus:'wrong',resolution:'Destroyed',noteCount:2,audioLoudness:trial().audioLoudness};
}
function noteV2(i=0) { const n={...note(i),schemaVersion:2,startedAtMs:i*600};delete n.requestedTimeInSequenceSeconds;return n; }
async function startedV2(u,notesPerSequence=2) {
  const s=`${u.path}/sessions/${randomUUID()}`;
  allowed(await commit(u,[write(s,{...session(u.receipt),schemaVersion:2,startedAtUnixMs:null,endedAtUnixMs:null,endReason:null},false)]));
  const t=startedTrial();t.settings.notesPerSequence=notesPerSequence;
  allowed(await commit(u,[write(`${s}/trials/0`,t,false)]));return {s,t};
}
function finalWrites(s,t) {return [write(`${s}/trials/0`,t),...Array.from({length:t.noteCount},(_,i)=>write(`${s}/trials/0/notes/${i}`,noteV2(i),false))];}

test('v2 start marker becomes one immutable result with notes, decision context and octave',async()=>{
  const u=await prepared(),{s,t}=await startedV2(u),final=resultTrial(t);
  allowed(await commit(u,finalWrites(s,final)));
  const actual=document(await read(u,`${s}/trials/0`));assert.equal(actual.secondDecision.atMs,350);assert.equal(actual.octaveShift,-1);
  assert.equal(document(await read(u,`${s}/trials/0/notes/1`)).startedAtMs,600);
  denied(await commit(u,[write(`${s}/trials/0`,final)]));
  denied(await commit(u,[write(`${s}/trials/0/notes/0`,noteV2())]));
  assert.equal((await call(`${db}/${s}/trials`,u.token,undefined,'GET')).body.documents.length,1);
  assert.equal((await call(`${db}/${s}/trials/0/notes`,u.token,undefined,'GET')).body.documents.length,2);
});
test('v2 rejects post-reveal second guesses, same-lane second guesses, invalid timing and extra identifying fields',async()=>{
  const u=await prepared(),{s,t}=await startedV2(u),f=resultTrial(t);
  for(const changes of [
    {secondDecision:{...f.secondDecision,atMs:701}}, {secondDecision:{...f.secondDecision,quadrant:0}},
    {firstDecision:{...f.firstDecision,mostRecentToneIndex:2}}, {cometSpawnedAtMs:1001},
    {settings:{...f.settings,email:'person@example.test'}}, {startedAtUnixMs:t.startedAtUnixMs+1},
    {octaveShift:5}, {predictionStatus:'correct'}, {participantName:'Person'}])
    denied(await commit(u,finalWrites(s,{...f,...changes})));
  const bad=finalWrites(s,f);bad[2]=write(`${s}/trials/0/notes/1`,{...noteV2(1),startedAtMs:1001},false);
  denied(await commit(u,bad));assert.equal(document(await read(u,`${s}/trials/0`)).state,'started');
});
test('v2 captures no-response timeout, radiation death, and zero-tone interruption without fake decisions',async()=>{
  for(const resolution of ['Hit','RadiationDeath','Interrupted']) {
    const u=await prepared(),{s,t}=await startedV2(u);
    const f=resolution==='Interrupted'?{...t,state:'resolved',resolvedAtMs:10,resolution,interruptionReason:'logout'}:
      {...resultTrial(t),firstDecision:null,secondDecision:null,predictionStatus:'notAttempted',resolution};
    allowed(await commit(u,finalWrites(s,f)));assert.equal(document(await read(u,`${s}/trials/0`)).firstDecision,null);
  }
});
test('v2 declined players cannot save even start markers; consent revocation blocks final notes; foreign players denied',async()=>{
  const d=await prepared('declined'),ds=await start(d);
  denied(await commit(d,[write(`${ds}/trials/0`,startedTrial(),false)]));
  const u=await prepared(),{s,t}=await startedV2(u),other=await prepared();
  denied(await read(other,`${s}/trials/0`));denied(await commit(other,finalWrites(s,resultTrial(t))));
  allowed(await saveConsent(u,consent('declined')));denied(await commit(u,finalWrites(s,resultTrial(t))));
  assert.equal(document(await read(u,`${s}/trials/0`)).state,'started');
});
test('v2 supports maximum-size atomic note sequence and rejects late note append',async()=>{
  const u=await prepared(),{s,t}=await startedV2(u,100);
  const f={...resultTrial(t),noteCount:100,resolvedAtMs:60000};allowed(await commit(u,finalWrites(s,f)));
  assert.equal((await call(`${db}/${s}/trials/0/notes?pageSize=100`,u.token,undefined,'GET')).body.documents.length,100);
  denied(await commit(u,[write(`${s}/trials/0/notes/100`,noteV2(100),false)]));
});

test('v2 rejects more than 100 planned notes and more notes than the saved trial planned',async()=>{
  const u=await prepared(),s=await start(u),oversize=startedTrial();oversize.settings.notesPerSequence=101;
  denied(await commit(u,[write(`${s}/trials/0`,oversize,false)]));
  const started=await startedV2(u),f={...resultTrial(started.t),noteCount:3,resolvedAtMs:1800};
  denied(await commit(u,finalWrites(started.s,f)));
});
