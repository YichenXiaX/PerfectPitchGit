export type Config = {
  supabaseUrl: string;
  publishableKey: string;
  allowedOrigins: string[];
  serviceRoleKey?: string;
};
type Fetch = typeof fetch;
type Route = { action: string; keys: string[] };
const routes: Record<string, Route> = {
  "GET profile": { action: "getProfile", keys: [] },
  "PUT profile": { action: "updateProfile", keys: ["nickname", "leaderboardEnabled"] },
  "POST sessions": { action: "startSession", keys: ["sessionId", "gameVersion", "mode"] },
  "PUT checkpoints": { action: "saveCheckpoint", keys: ["sessionId", "revision", "score", "highestLevel", "elapsedSeconds", "completed"] },
  "GET progress": { action: "getProgress", keys: [] },
  "GET history": { action: "getHistory", keys: ["limit", "offset"] },
  "GET leaderboard": { action: "getLeaderboard", keys: ["gameVersion"] },
};
class ApiError extends Error {
  status: number;
  code: string;
  constructor(status: number, code: string) { super(code); this.status = status; this.code = code; }
}
function exactKeys(value: unknown, keys: string[]): asserts value is Record<string, unknown> {
  if (!value || typeof value !== "object" || Array.isArray(value) ||
    Object.keys(value).length !== keys.length || Object.keys(value).some(k => !keys.includes(k))) {
    throw new ApiError(400, "unexpected_or_missing_fields");
  }
}
function integer(value: unknown, min: number, max: number) {
  return typeof value === "number" && Number.isSafeInteger(value) && value >= min && value <= max;
}
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const version = /^[A-Za-z0-9._-]{1,40}$/;
export function validate(action: string, value: Record<string, unknown>): void {
  const text = (key: string, regex: RegExp) => typeof value[key] === "string" && regex.test(value[key] as string);
  let valid = true;
  if (action === "updateProfile") valid = text("nickname", /^[A-Za-z][A-Za-z0-9_-]{2,23}$/) && typeof value.leaderboardEnabled === "boolean";
  if (action === "startSession") valid = text("sessionId", uuid) && text("gameVersion", version) && ["standard", "custom"].includes(value.mode as string);
  if (action === "saveCheckpoint") valid = text("sessionId", uuid) && integer(value.revision, 1, 100000) &&
    integer(value.score, -1e9, 1e9) && integer(value.highestLevel, 0, 3) && integer(value.elapsedSeconds, 0, 86400) &&
    typeof value.completed === "boolean" && Math.abs(value.score as number) <= 500000 * ((value.elapsedSeconds as number) + 1);
  if (action === "getHistory") valid = integer(value.limit, 1, 50) && integer(value.offset, 0, 100000);
  if (action === "getLeaderboard") valid = text("gameVersion", version);
  if (!valid) throw new ApiError(400, "invalid_values");
}
async function body(request: Request): Promise<unknown> {
  if (!request.headers.get("content-type")?.toLowerCase().startsWith("application/json"))
    throw new ApiError(415, "json_required");
  const reader = request.body?.getReader();
  if (!reader) throw new ApiError(400, "body_required");
  const chunks: Uint8Array[] = [];
  let length = 0;
  while (true) {
    const part = await reader.read();
    if (part.done) break;
    length += part.value.length;
    if (length > 4096) { await reader.cancel(); throw new ApiError(413, "payload_too_large"); }
    chunks.push(part.value);
  }
  const bytes = new Uint8Array(length);
  let offset = 0;
  for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.length; }
  try { return JSON.parse(new TextDecoder("utf-8", { fatal: true }).decode(bytes)); }
  catch { throw new ApiError(400, "invalid_json"); }
}
export function createHandler(config: Config, fetcher: Fetch = fetch) {
  const base = config.supabaseUrl.replace(/\/$/, "");
  return async (request: Request): Promise<Response> => {
    const origin = request.headers.get("origin");
    const headers = new Headers({
      "Content-Type": "application/json",
      "Cache-Control": "no-store",
      "Vary": "Origin",
      "X-Content-Type-Options": "nosniff",
    });
    if (origin && config.allowedOrigins.includes(origin)) headers.set("Access-Control-Allow-Origin", origin);
    const reply = (status: number, value: unknown) => new Response(JSON.stringify(value), { status, headers });
    try {
      if (!base || !config.publishableKey || !config.allowedOrigins.length)
        throw new ApiError(503, "server_not_configured");
      if (origin && !config.allowedOrigins.includes(origin)) throw new ApiError(403, "origin_not_allowed");
      if (request.method === "OPTIONS") {
        headers.set("Access-Control-Allow-Methods", "GET,POST,PUT,DELETE,OPTIONS");
        headers.set("Access-Control-Allow-Headers", "authorization,apikey,content-type,x-client-info");
        headers.set("Access-Control-Max-Age", "600");
        return new Response(null, { status: 204, headers });
      }
      const url = new URL(request.url);
      const path = url.pathname.replace(/^.*\/game-api\/?/, "").replace(/^\//, "");
      const route = routes[request.method + " " + path];
      const deleting = request.method === "DELETE" && path === "account";
      if (!route && !deleting) throw new ApiError(404, "route_not_found");
      const authorization = request.headers.get("authorization") ?? "";
      if (!/^Bearer [^\s]+$/i.test(authorization)) throw new ApiError(401, "login_required");
      // Verify with Auth, never decode an unverified JWT and trust its user ID.
      const authResponse = await fetcher(base + "/auth/v1/user", {
        headers: { apikey: config.publishableKey, Authorization: authorization },
        signal: AbortSignal.timeout(10000),
      });
      if (!authResponse.ok) {
        throw new ApiError(authResponse.status >= 500 ? 503 : 401, authResponse.status >= 500 ? "auth_unavailable" : "invalid_session");
      }
      const user = await authResponse.json();
      if (typeof user.id !== "string" || !uuid.test(user.id) || !user.email_confirmed_at || !user.email || user.is_anonymous === true)
        throw new ApiError(403, "verified_account_required");

      let payload: unknown;
      if (request.method === "GET") {
        const keys = Array.from(url.searchParams.keys());
        if (new Set(keys).size !== keys.length) throw new ApiError(400, "duplicate_query_parameter");
        payload = Object.fromEntries(url.searchParams);
        if (route?.action === "getHistory") {
          const q = payload as Record<string, unknown>;
          if (!/^[0-9]+$/.test(String(q.limit)) || !/^[0-9]+$/.test(String(q.offset)))
            throw new ApiError(400, "invalid_pagination");
          q.limit = Number(q.limit); q.offset = Number(q.offset);
        }
      } else {
        if (url.search) throw new ApiError(400, "unexpected_query_parameters");
        payload = await body(request);
      }
      if (deleting) {
        exactKeys(payload, ["confirmation"]);
        if (payload.confirmation !== "DELETE") throw new ApiError(400, "deletion_confirmation_required");
        if (!config.serviceRoleKey) throw new ApiError(503, "account_deletion_not_configured");
        // The target ID comes only from verified Auth; callers cannot choose another account.
        const deleted = await fetcher(base + "/auth/v1/admin/users/" + user.id, {
          method: "DELETE",
          headers: { apikey: config.serviceRoleKey, Authorization: "Bearer " + config.serviceRoleKey, "Content-Type": "application/json" },
          body: JSON.stringify({ should_soft_delete: false }),
          signal: AbortSignal.timeout(10000),
        });
        if (!deleted.ok) throw new ApiError(503, "account_deletion_failed");
        return reply(200, { deleted: true });
      }
      exactKeys(payload, route!.keys);
      validate(route!.action, payload);
      const response = await fetcher(base + "/rest/v1/rpc/game_api", {
        method: "POST",
        headers: { apikey: config.publishableKey, Authorization: authorization, "Content-Type": "application/json" },
        body: JSON.stringify({ p_action: route!.action, p_payload: payload }),
        signal: AbortSignal.timeout(10000),
      });
      const result = await response.json();
      if (!response.ok) {
        if (result.message === "RATE_LIMIT") throw new ApiError(429, "rate_limit");
        if (result.message === "SESSION_NOT_FOUND") throw new ApiError(404, "session_not_found");
        if (["SESSION_CONFLICT", "CHECKPOINT_CONFLICT"].includes(result.message)) throw new ApiError(409, "save_conflict");
        if (result.code === "23505") throw new ApiError(409, route!.action === "updateProfile" ? "nickname_taken" : "save_conflict");
        if (result.code === "42501") throw new ApiError(403, "account_not_authorized");
        if (["22023", "22003", "22P02", "23514"].includes(result.code)) throw new ApiError(400, "invalid_values");
        throw new ApiError(503, "storage_unavailable");
      }
      return reply(200, result);
    } catch (error) {
      // Never log bodies, Authorization headers, names, contact details, or raw provider errors.
      if (error instanceof ApiError) {
        if (error.status === 429) headers.set("Retry-After", "60");
        return reply(error.status, { error: error.code });
      }
      return reply(503, { error: "service_unavailable" });
    }
  };
}
