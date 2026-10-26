import { test } from "node:test";
import assert from "node:assert/strict";
import { createHandler } from "../supabase/functions/game-api/handler.ts";
const userId="11111111-1111-4111-8111-111111111111";
const config={supabaseUrl:"https://example.supabase.co",publishableKey:"public-key",allowedOrigins:["https://game.example"],serviceRoleKey:"server-only"};
function fixture(user: object = {id:userId,email:"player@example.invalid",email_confirmed_at:"2026-01-01",is_anonymous:false}, rpcStatus=200, rpc: object={ok:true}) {
  const calls:{url:string;options:RequestInit|undefined}[]=[];
  const handler=createHandler(config,async (url,options)=>{
    calls.push({url:String(url),options});
    if(String(url).endsWith("/auth/v1/user")) return Response.json(user);
    return Response.json(rpc,{status:rpcStatus});
  });
  return {handler,calls};
}
function request(path:string,method="GET",payload?:unknown,extra:Record<string,string>={}) {
  return new Request("https://example.supabase.co/functions/v1/game-api/"+path,{
    method,headers:{Origin:"https://game.example",Authorization:"Bearer player-token",...(payload===undefined?{}:{"Content-Type":"application/json"}),...extra},
    ...(payload===undefined?{}:{body:JSON.stringify(payload)}),
  });
}
test("CORS allowlist and safe preflight",async()=>{
  const f=fixture();
  assert.equal((await f.handler(request("profile","GET",undefined,{Origin:"https://evil.example"}))).status,403);
  const preflight=await f.handler(request("profile","OPTIONS"));
  assert.equal(preflight.status,204);assert.equal(f.calls.length,0);
  assert.equal(preflight.headers.get("Access-Control-Allow-Origin"),"https://game.example");
});
test("requires verified non-anonymous identity before storage access",async()=>{
  const f=fixture();
  assert.equal((await f.handler(request("profile","GET",undefined,{Authorization:""}))).status,401);
  assert.equal(f.calls.length,0);
  for(const user of [{id:userId,is_anonymous:true,email:"x",email_confirmed_at:"yes"},{id:userId,email:"x"},{id:"forged",email:"x",email_confirmed_at:"yes"}]){
    const x=fixture(user);assert.equal((await x.handler(request("profile"))).status,403);assert.equal(x.calls.length,1);
  }
});
test("never forwards detailed or unexpected fields to storage",async()=>{
  for(const payload of [
    {sessionId:userId,revision:1,score:0,highestLevel:0,elapsedSeconds:0,completed:false,results:[]},
    {nickname:"Tester",leaderboardEnabled:false,contact:"private"},
  ]){
    const f=fixture();const path="results" in payload?"checkpoints":"profile";
    assert.equal((await f.handler(request(path,"PUT",payload))).status,400);assert.equal(f.calls.length,1);
  }
  assert.equal((await fixture().handler(request("research","POST",{consent:true}))).status,404);
});
test("payload size, content type and query validation",async()=>{
  const f=fixture();
  assert.equal((await f.handler(request("profile","PUT",{nickname:"x".repeat(5000)}))).status,413);
  assert.equal((await f.handler(request("history?limit=10&limit=20&offset=0"))).status,400);
  assert.equal((await f.handler(request("history?limit=NaN&offset=0"))).status,400);
  assert.equal((await f.handler(request("profile?contact=secret"))).status,400);
  assert.equal((await f.handler(request("profile","PUT",{},{"Content-Type":"text/plain"}))).status,415);
});
test("database requests preserve user identity and do not use admin credentials",async()=>{
  const f=fixture();const response=await f.handler(request("profile","PUT",{nickname:"Tester",leaderboardEnabled:false}));
  assert.equal(response.status,200);assert.equal(response.headers.get("Cache-Control"),"no-store");
  const rpc=f.calls[1];assert.match(rpc.url,/rest\/v1\/rpc\/game_api$/);
  assert.equal(new Headers(rpc.options?.headers).get("Authorization"),"Bearer player-token");
  assert.equal(new Headers(rpc.options?.headers).get("apikey"),"public-key");
});
test("provider errors are sanitized",async()=>{
  for(const [status,error,expected] of [[400,{message:"CHECKPOINT_CONFLICT"},409],[400,{message:"RATE_LIMIT"},429],[500,{message:"secret connection string"},503]] as const){
    const response=await fixture(undefined,status,error).handler(request("progress"));
    assert.equal(response.status,expected);assert.ok(!(await response.text()).includes("secret connection string"));
  }
});
test("account deletion needs confirmation and cannot target another identity",async()=>{
  const f=fixture();
  assert.equal((await f.handler(request("account","DELETE",{confirmation:"DELETE",userId:"someoneElse"}))).status,400);
  assert.equal(f.calls.length,1);
  assert.equal((await f.handler(request("account","DELETE",{confirmation:"DELETE"}))).status,200);
  assert.equal(f.calls.at(-1)?.url,config.supabaseUrl+"/auth/v1/admin/users/"+userId);
  assert.equal(new Headers(f.calls.at(-1)?.options?.headers).get("Authorization"),"Bearer server-only");
});
test("network errors give a retryable response without internal details",async()=>{
  const handler=createHandler(config,async()=>{throw new Error("private diagnostic");});
  const response=await handler(request("profile"));
  assert.equal(response.status,503);assert.deepEqual(await response.json(),{error:"service_unavailable"});
});
