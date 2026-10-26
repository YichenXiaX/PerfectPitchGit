import { createHandler } from "./handler.ts";

const required = (name: string) => {
  const value = Deno.env.get(name);
  if (!value) throw new Error("Missing required server configuration: " + name);
  return value;
};
Deno.serve(createHandler({
  supabaseUrl: required("SUPABASE_URL"),
  // Supabase supplies the legacy anon key automatically; a publishable key may override it.
  publishableKey: Deno.env.get("GAME_PUBLISHABLE_KEY") ?? required("SUPABASE_ANON_KEY"),
  allowedOrigins: required("ALLOWED_ORIGINS").split(",").map(value => value.trim()).filter(Boolean),
  serviceRoleKey: Deno.env.get("SUPABASE_SERVICE_ROLE_KEY"),
}));
