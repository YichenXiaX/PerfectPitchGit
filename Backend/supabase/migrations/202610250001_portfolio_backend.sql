-- Portfolio release: summaries only. There is deliberately no trial, consent, or contact table.
begin;
create schema if not exists game_private;
revoke all on schema game_private from public, anon, authenticated;

create table game_private.profiles (
  user_id uuid primary key references auth.users(id) on delete cascade,
  nickname text not null check (nickname ~ '^[A-Za-z][A-Za-z0-9_-]{2,23}$'),
  leaderboard_enabled boolean not null default false,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);
create unique index profiles_nickname on game_private.profiles (lower(nickname));

create table game_private.sessions (
  session_id uuid primary key,
  user_id uuid not null references auth.users(id) on delete cascade,
  game_version text not null check (game_version ~ '^[A-Za-z0-9._-]{1,40}$'),
  mode text not null check (mode in ('standard','custom')),
  revision integer not null default 0,
  score bigint not null default 0,
  highest_level integer not null default 0,
  elapsed_seconds integer not null default 0,
  completed boolean not null default false,
  started_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  completed_at timestamptz,
  check (revision between 0 and 100000),
  check (score between -1000000000 and 1000000000),
  check (highest_level between 0 and 3),
  check (elapsed_seconds between 0 and 86400)
);
create index sessions_owner on game_private.sessions(user_id, started_at desc);
create index sessions_leaderboard on game_private.sessions(score desc) where completed and mode='standard';
create table game_private.request_limits (
  user_id uuid primary key references auth.users(id) on delete cascade,
  window_started_at timestamptz not null,
  request_count integer not null
);
alter table game_private.profiles enable row level security;
alter table game_private.sessions enable row level security;
alter table game_private.request_limits enable row level security;
revoke all on all tables in schema game_private from public, anon, authenticated;

create function game_private.expect_keys(value jsonb, allowed text[]) returns void
language plpgsql set search_path='' as $$
begin
  if value is null or jsonb_typeof(value) <> 'object'
    or (select count(*) from jsonb_object_keys(value)) <> cardinality(allowed)
    or exists(select 1 from jsonb_object_keys(value) k where not(k=any(allowed))) then
    raise exception using errcode='22023', message='Unexpected or missing fields';
  end if;
end $$;

create function game_private.session_json(s game_private.sessions) returns jsonb
language sql immutable set search_path='' as $$
  select jsonb_build_object('sessionId',s.session_id,'gameVersion',s.game_version,'mode',s.mode,
    'revision',s.revision,'score',s.score,'highestLevel',s.highest_level,
    'elapsedSeconds',s.elapsed_seconds,'completed',s.completed,
    'startedAt',s.started_at,'updatedAt',s.updated_at,'completedAt',s.completed_at);
$$;

-- This is the only gameplay write/read entrypoint. Auth is checked again here so bypassing
-- the Edge Function and calling PostgREST directly does not bypass ownership or validation.
create function public.game_api(p_action text, p_payload jsonb default '{}'::jsonb)
returns jsonb language plpgsql security definer set search_path='' as $$
declare
  player uuid := auth.uid();
  profile game_private.profiles;
  saved game_private.sessions;
  session_id_value uuid;
  new_revision integer;
  new_score bigint;
  new_level integer;
  new_elapsed integer;
  new_completed boolean;
  request_count_value integer;
  page_limit integer;
  page_offset integer;
  rows_json jsonb;
begin
  if player is null or not exists(
    select 1 from auth.users where id=player and email_confirmed_at is not null
      and email is not null and not coalesce(is_anonymous,false)
  ) then
    raise exception using errcode='42501', message='A verified account is required';
  end if;
  if p_payload is null or jsonb_typeof(p_payload)<>'object' or octet_length(p_payload::text)>4096 then
    raise exception using errcode='22023', message='Invalid payload';
  end if;

  insert into game_private.request_limits as r values(player,now(),1)
  on conflict(user_id) do update set
    window_started_at=case when r.window_started_at <= now()-interval '1 minute' then now() else r.window_started_at end,
    request_count=case when r.window_started_at <= now()-interval '1 minute' then 1 else r.request_count+1 end
  returning request_count into request_count_value;
  if request_count_value>120 then
    raise exception using errcode='P0001', message='RATE_LIMIT';
  end if;
  -- First authenticated request creates a nickname without copying email or Auth metadata.
  insert into game_private.profiles(user_id,nickname)
    values(player,'player_'||substr(replace(player::text,'-',''),1,16)) on conflict(user_id) do nothing;
  select * into profile from game_private.profiles where user_id=player;

  if p_action='getProfile' then
    perform game_private.expect_keys(p_payload,array[]::text[]);
  elsif p_action='updateProfile' then
    perform game_private.expect_keys(p_payload,array['nickname','leaderboardEnabled']);
    if jsonb_typeof(p_payload->'nickname')<>'string' or (p_payload->>'nickname') !~ '^[A-Za-z][A-Za-z0-9_-]{2,23}$'
      or jsonb_typeof(p_payload->'leaderboardEnabled')<>'boolean' then
      raise exception using errcode='22023',message='Invalid profile';
    end if;
    update game_private.profiles set nickname=p_payload->>'nickname',
      leaderboard_enabled=(p_payload->>'leaderboardEnabled')::boolean,updated_at=now()
      where user_id=player returning * into profile;
  elsif p_action='startSession' then
    perform game_private.expect_keys(p_payload,array['sessionId','gameVersion','mode']);
    if jsonb_typeof(p_payload->'sessionId')<>'string' or (p_payload->>'sessionId') !~* '^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$'
      or jsonb_typeof(p_payload->'gameVersion')<>'string' or (p_payload->>'gameVersion') !~ '^[A-Za-z0-9._-]{1,40}$'
      or jsonb_typeof(p_payload->'mode')<>'string' or (p_payload->>'mode') not in ('standard','custom') then
      raise exception using errcode='22023',message='Invalid session';
    end if;
    session_id_value=(p_payload->>'sessionId')::uuid;
    select * into saved from game_private.sessions where session_id=session_id_value;
    if found then
      if saved.user_id<>player or saved.game_version<>p_payload->>'gameVersion' or saved.mode<>p_payload->>'mode' then
        raise exception using errcode='P0001',message='SESSION_CONFLICT';
      end if;
      return game_private.session_json(saved);
    end if;
    if (select count(*) from game_private.sessions where user_id=player and started_at>now()-interval '1 hour')>=20 then
      raise exception using errcode='P0001',message='RATE_LIMIT';
    end if;
    insert into game_private.sessions(session_id,user_id,game_version,mode)
      values(session_id_value,player,p_payload->>'gameVersion',p_payload->>'mode')
      returning * into saved;
    return game_private.session_json(saved);
  elsif p_action='saveCheckpoint' then
    perform game_private.expect_keys(p_payload,array['sessionId','revision','score','highestLevel','elapsedSeconds','completed']);
    if jsonb_typeof(p_payload->'sessionId')<>'string' or (p_payload->>'sessionId') !~* '^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$'
      or jsonb_typeof(p_payload->'completed')<>'boolean' then
      raise exception using errcode='22023',message='Invalid checkpoint';
    end if;
    if exists(select 1 from unnest(array['revision','score','highestLevel','elapsedSeconds']) k
      where jsonb_typeof(p_payload->k)<>'number' or (p_payload->>k) !~ '^-?[0-9]{1,10}$') then
      raise exception using errcode='22023',message='Expected bounded integers';
    end if;
    new_revision=(p_payload->>'revision')::integer; new_score=(p_payload->>'score')::bigint;
    new_level=(p_payload->>'highestLevel')::integer; new_elapsed=(p_payload->>'elapsedSeconds')::integer;
    new_completed=(p_payload->>'completed')::boolean;
    if new_revision not between 1 and 100000 or new_score not between -1000000000 and 1000000000
      or new_level not between 0 and 3 or new_elapsed not between 0 and 86400
      or abs(new_score)>500000::bigint*(new_elapsed+1) then
      raise exception using errcode='22023',message='Checkpoint outside limits';
    end if;
    select * into saved from game_private.sessions where session_id=(p_payload->>'sessionId')::uuid and user_id=player for update;
    if not found then raise exception using errcode='P0001',message='SESSION_NOT_FOUND';end if;
    if new_revision=saved.revision and new_score=saved.score and new_level=saved.highest_level
      and new_elapsed=saved.elapsed_seconds and new_completed=saved.completed then
      return game_private.session_json(saved);
    end if;
    if saved.completed or new_revision<=saved.revision or new_elapsed<saved.elapsed_seconds or new_level<saved.highest_level then
      raise exception using errcode='P0001',message='CHECKPOINT_CONFLICT';
    end if;
    if new_elapsed>extract(epoch from(now()-saved.started_at))+5 or now()-saved.started_at>interval '24 hours' then
      raise exception using errcode='22023',message='Invalid session duration';
    end if;
    update game_private.sessions set revision=new_revision,score=new_score,highest_level=new_level,
      elapsed_seconds=new_elapsed,completed=new_completed,updated_at=now(),
      completed_at=case when new_completed then now() else null end
      where session_id=saved.session_id returning * into saved;
    return game_private.session_json(saved);
  elsif p_action='getProgress' then
    perform game_private.expect_keys(p_payload,array[]::text[]);
    return (select jsonb_build_object('bestScore',coalesce(max(score),0),
      'highestLevel',coalesce(max(highest_level),0),'sessionsPlayed',count(*),
      'sessionsCompleted',count(*) filter(where completed))
      from game_private.sessions where user_id=player);
  elsif p_action='getHistory' then
    perform game_private.expect_keys(p_payload,array['limit','offset']);
    if jsonb_typeof(p_payload->'limit')<>'number' or (p_payload->>'limit') !~ '^[0-9]{1,2}$'
      or jsonb_typeof(p_payload->'offset')<>'number' or (p_payload->>'offset') !~ '^[0-9]{1,6}$' then
      raise exception using errcode='22023',message='Invalid pagination';
    end if;
    page_limit=(p_payload->>'limit')::integer;page_offset=(p_payload->>'offset')::integer;
    if page_limit not between 1 and 50 or page_offset>100000 then raise exception using errcode='22023',message='Invalid pagination';end if;
    select coalesce(jsonb_agg(game_private.session_json(s)),'[]'::jsonb) into rows_json from(
      select * from game_private.sessions where user_id=player order by started_at desc,session_id desc limit page_limit offset page_offset
    ) s;
    return jsonb_build_object('sessions',rows_json,'limit',page_limit,'offset',page_offset);
  elsif p_action='getLeaderboard' then
    perform game_private.expect_keys(p_payload,array['gameVersion']);
    if jsonb_typeof(p_payload->'gameVersion')<>'string' or (p_payload->>'gameVersion') !~ '^[A-Za-z0-9._-]{1,40}$' then
      raise exception using errcode='22023',message='Invalid game version';
    end if;
    select coalesce(jsonb_agg(jsonb_build_object('rank',r.place,'nickname',r.nickname,'score',r.score)),'[]'::jsonb)
      into rows_json from(
        select row_number() over(order by max(s.score) desc,p.nickname) as place,p.nickname,max(s.score) as score
        from game_private.sessions s join game_private.profiles p on p.user_id=s.user_id
        where p.leaderboard_enabled and s.completed and s.mode='standard' and s.game_version=p_payload->>'gameVersion'
        group by p.user_id,p.nickname order by max(s.score) desc,p.nickname limit 100
      ) r;
    return jsonb_build_object('entries',rows_json,'verification','clientReported','gameVersion',p_payload->>'gameVersion');
  else
    raise exception using errcode='22023',message='Unsupported action; research collection is disabled';
  end if;
  return jsonb_build_object('nickname',profile.nickname,'leaderboardEnabled',profile.leaderboard_enabled,
    'createdAt',profile.created_at,'researchCollectionEnabled',false);
end $$;
revoke all on all functions in schema game_private from public, anon, authenticated;
revoke all on function public.game_api(text,jsonb) from public, anon, authenticated;
grant execute on function public.game_api(text,jsonb) to authenticated;
comment on function public.game_api(text,jsonb) is 'Verified-account, summary-only portfolio API. No research payloads accepted.';
commit;
