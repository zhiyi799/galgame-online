-- GalgameOnline — 0003_functions
-- Server-side game functions. All are SECURITY DEFINER with a fixed
-- search_path, so they run with the function owner's privileges (the
-- postgres role that applied the migration) regardless of the caller.
-- Only the service_role may execute them — the Worker calls them through
-- PostgREST with the service_role key. The authenticated/anon roles have
-- no EXECUTE grant, so even a client that reaches Supabase directly cannot
-- call them.
--
-- Concurrency: do_gacha_pull and claim_mail lock the rows they mutate
-- (SELECT ... FOR UPDATE) inside a single transaction, so two simultaneous
-- pulls for one account cannot double-spend. This is the Postgres
-- equivalent of the C# prototype's per-account SemaphoreSlim locks.
-- do_gacha_pull additionally uses optimistic concurrency: the Worker passes
-- the state it read (p_expected_state); if another pull committed first,
-- the function returns {ok:false, error:'state_changed'} and the Worker
-- retries the whole pull (max 3 attempts).

-- Get-or-create wallet (starter grant 0 premium / 1000 free / 0 tickets,
-- same as the C# prototype).
create or replace function public.ensure_wallet(p_user_id uuid)
returns public.wallets
language plpgsql
security definer
set search_path = public
as $$
declare
  v_wallet public.wallets;
begin
  insert into public.wallets (user_id, premium, free, tickets)
    values (p_user_id, 0, 1000, 0)
    on conflict (user_id) do nothing;
  select * into v_wallet from public.wallets where user_id = p_user_id;
  return v_wallet;
end;
$$;

-- Atomic gacha pull commit. The draw itself runs in the Worker (TypeScript
-- port of the C# GachaEngine); this function applies the result atomically:
-- re-validates the daily limit and funds against row-locked data, deducts
-- the currency (split premium-first, computed by the Worker), persists the
-- new pity state, and grants the pulled items to the inventory.
-- p_expected_state / p_new_state are the full gacha-state JSON:
-- {total, since_five, since_four, featured_since, guaranteed, daily_count,
--  daily_anchor}. p_outcomes is [{item_id, kind, count}].
create or replace function public.do_gacha_pull(
  p_user_id uuid,
  p_banner_id smallint,
  p_count smallint,
  p_cost_premium bigint,
  p_cost_free bigint,
  p_expected_state jsonb,
  p_new_state jsonb,
  p_outcomes jsonb
)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
  v_daily_limit integer;
  v_wallet record;
  v_state record;
  v_today date := (now() at time zone 'utc')::date;
  v_outcome jsonb;
begin
  select daily_limit into v_daily_limit
    from public.banners where id = p_banner_id and active;
  if not found then
    return jsonb_build_object('ok', false, 'error',
      format('unknown banner %s', p_banner_id));
  end if;

  -- Lock the wallet (create the starter wallet if this is a first pull).
  insert into public.wallets (user_id, premium, free, tickets)
    values (p_user_id, 0, 1000, 0)
    on conflict (user_id) do nothing;
  select * into v_wallet from public.wallets where user_id = p_user_id for update;

  -- Lock the gacha state (create a fresh row if this is a first pull).
  insert into public.gacha_state
    (user_id, banner_id, total, since_five, since_four, featured_since,
     guaranteed, daily_count, daily_anchor)
    values (p_user_id, p_banner_id, 0, 0, 0, 0, false, 0, now())
    on conflict (user_id, banner_id) do nothing;
  select * into v_state from public.gacha_state
    where user_id = p_user_id and banner_id = p_banner_id for update;

  -- Daily reset, same rule as the engine: a later UTC day resets the counter.
  if (v_state.daily_anchor at time zone 'utc')::date < v_today then
    v_state.daily_count := 0;
    v_state.daily_anchor := now();
  end if;

  -- Optimistic concurrency: the Worker computed from the state it read.
  -- A mismatch means another pull committed first -> the Worker retries.
  -- daily_anchor is compared by UTC day only (timestamps differ by ms).
  if v_state.total <> (p_expected_state ->> 'total')::int
     or v_state.since_five <> (p_expected_state ->> 'since_five')::int
     or v_state.since_four <> (p_expected_state ->> 'since_four')::int
     or v_state.featured_since <> (p_expected_state ->> 'featured_since')::int
     or v_state.guaranteed <> (p_expected_state ->> 'guaranteed')::boolean
     or v_state.daily_count <> (p_expected_state ->> 'daily_count')::int
     or ((p_expected_state ->> 'daily_anchor') is not null
         and (v_state.daily_anchor at time zone 'utc')::date
             <> ((p_expected_state ->> 'daily_anchor')::timestamptz at time zone 'utc')::date)
  then
    return jsonb_build_object('ok', false, 'error', 'state_changed');
  end if;

  -- Re-validate against the locked rows (defense in depth + TOCTOU safety).
  if v_daily_limit > 0 and v_state.daily_count + p_count > v_daily_limit then
    return jsonb_build_object('ok', false, 'error', 'daily pull limit reached');
  end if;
  if v_wallet.premium + v_wallet.free < p_cost_premium + p_cost_free then
    return jsonb_build_object('ok', false, 'error', 'insufficient currency');
  end if;

  -- Spend (premium first — the split was computed by the Worker).
  update public.wallets
    set premium = premium - p_cost_premium,
        free = free - p_cost_free,
        updated_at = now()
    where user_id = p_user_id;

  -- Persist the new pity state computed by the Worker.
  update public.gacha_state
    set total = (p_new_state ->> 'total')::int,
        since_five = (p_new_state ->> 'since_five')::int,
        since_four = (p_new_state ->> 'since_four')::int,
        featured_since = (p_new_state ->> 'featured_since')::int,
        guaranteed = (p_new_state ->> 'guaranteed')::boolean,
        daily_count = (p_new_state ->> 'daily_count')::int,
        daily_anchor = (p_new_state ->> 'daily_anchor')::timestamptz
    where user_id = p_user_id and banner_id = p_banner_id;

  -- Grant the pulled items (duplicates in one 10-pull aggregate).
  for v_outcome in select * from jsonb_array_elements(p_outcomes) loop
    insert into public.inventory (user_id, item_id, kind, count, level)
      values (p_user_id, (v_outcome ->> 'item_id')::int, v_outcome ->> 'kind',
              (v_outcome ->> 'count')::int, 1)
      on conflict (user_id, item_id) do update
        set count = public.inventory.count + excluded.count;
  end loop;

  return jsonb_build_object('ok', true);
end;
$$;

-- Claim a mail: marks it claimed+read and grants its attachments atomically.
-- Currency attachments (kind premium/free/ticket) go to the wallet, anything
-- else goes to the inventory. Attachments use the C# DTO shape
-- [{itemId, kind, count}] (camelCase).
create or replace function public.claim_mail(p_user_id uuid, p_mail_id uuid)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
  v_mail record;
  v_att jsonb;
begin
  select * into v_mail from public.mail where id = p_mail_id for update;
  if not found then
    return jsonb_build_object('ok', false, 'error', 'mail not found');
  end if;
  if v_mail.user_id <> p_user_id then
    return jsonb_build_object('ok', false, 'error', 'mail does not belong to this account');
  end if;
  if v_mail.claimed then
    return jsonb_build_object('ok', false, 'error', 'mail already claimed');
  end if;

  update public.mail set claimed = true, read = true where id = p_mail_id;

  for v_att in select * from jsonb_array_elements(coalesce(v_mail.attachments, '[]'::jsonb)) loop
    if (v_att ->> 'kind') in ('premium', 'free', 'ticket') then
      insert into public.wallets (user_id, premium, free, tickets)
        values (p_user_id, 0, 1000, 0)
        on conflict (user_id) do nothing;
      update public.wallets
        set premium = premium + case when v_att ->> 'kind' = 'premium' then (v_att ->> 'count')::int else 0 end,
            free = free + case when v_att ->> 'kind' = 'free' then (v_att ->> 'count')::int else 0 end,
            tickets = tickets + case when v_att ->> 'kind' = 'ticket' then (v_att ->> 'count')::int else 0 end,
            updated_at = now()
        where user_id = p_user_id;
    else
      insert into public.inventory (user_id, item_id, kind, count, level)
        values (p_user_id, (v_att ->> 'itemId')::int, v_att ->> 'kind',
                (v_att ->> 'count')::int, 1)
        on conflict (user_id, item_id) do update
          set count = public.inventory.count + excluded.count;
    end if;
  end loop;

  return jsonb_build_object('ok', true, 'granted', coalesce(v_mail.attachments, '[]'::jsonb));
end;
$$;

-- Write a cloud save slot (1-3). Upsert: re-uploading overwrites the blob.
create or replace function public.upsert_cloud_save(
  p_user_id uuid,
  p_slot smallint,
  p_data text
)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
begin
  if p_slot < 1 or p_slot > 3 then
    return jsonb_build_object('ok', false, 'error', 'slot must be 1-3');
  end if;
  insert into public.cloud_saves (user_id, slot, data_json, updated_at)
    values (p_user_id, p_slot, p_data, now())
    on conflict (user_id, slot) do update
      set data_json = excluded.data_json, updated_at = now();
  return jsonb_build_object('ok', true);
end;
$$;

-- Dev/admin helper: send a mail to a user. In production this is called by
-- server jobs (events, compensation), not by players — the Worker route that
-- exposes it is gated behind ALLOW_DEV_MAIL_SEND.
create or replace function public.send_mail(
  p_user_id uuid,
  p_title text,
  p_body text,
  p_attachments jsonb
)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
  v_id uuid;
begin
  insert into public.mail (user_id, title, body, attachments)
    values (p_user_id, p_title, p_body, coalesce(p_attachments, '[]'::jsonb))
    returning id into v_id;
  return jsonb_build_object('ok', true, 'mail_id', v_id);
end;
$$;

-- Only the service role may execute these functions. The Worker calls them
-- through PostgREST with the service_role key; authenticated/anon clients
-- get no EXECUTE grant.
revoke all on function public.ensure_wallet(uuid) from public;
revoke all on function public.do_gacha_pull(uuid, smallint, smallint, bigint, bigint, jsonb, jsonb, jsonb) from public;
revoke all on function public.claim_mail(uuid, uuid) from public;
revoke all on function public.upsert_cloud_save(uuid, smallint, text) from public;
revoke all on function public.send_mail(uuid, text, text, jsonb) from public;

grant execute on function public.ensure_wallet(uuid) to service_role;
grant execute on function public.do_gacha_pull(uuid, smallint, smallint, bigint, bigint, jsonb, jsonb, jsonb) to service_role;
grant execute on function public.claim_mail(uuid, uuid) to service_role;
grant execute on function public.upsert_cloud_save(uuid, smallint, text) to service_role;
grant execute on function public.send_mail(uuid, text, text, jsonb) to service_role;
