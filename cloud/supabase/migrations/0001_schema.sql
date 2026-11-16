-- GalgameOnline — 0001_schema
-- Game-state tables. All writes go through security-definer RPC functions
-- (0003_functions.sql); the anon/authenticated roles can only SELECT their own
-- rows (see 0002_policies.sql). The Unity/HTML clients never touch these
-- tables directly — only the Cloudflare Worker does, with the service role.
--
-- Naming follows the C# prototype's models (GalgameOnline.Shared/Models.cs):
-- profiles ~ Account, wallets ~ Wallet, gacha_state ~ GachaState,
-- inventory ~ InventoryItem, mail ~ MailItem, cloud_saves ~ CloudSave.
-- Auth identity is Supabase Auth (auth.users.id); usernames live in profiles.

create extension if not exists "pgcrypto";

-- Public profile for a Supabase Auth user. Created by the Worker after signup.
create table if not exists public.profiles (
  id uuid primary key references auth.users (id) on delete cascade,
  username text not null unique,
  created_at timestamptz not null default now(),
  last_login_at timestamptz
);

-- Currency balances. Starter grant is 0 premium / 1000 free / 0 tickets
-- (same as the C# prototype's GetOrCreateWalletAsync).
create table if not exists public.wallets (
  user_id uuid primary key references auth.users (id) on delete cascade,
  premium bigint not null default 0 check (premium >= 0),
  free bigint not null default 0 check (free >= 0),
  tickets bigint not null default 0 check (tickets >= 0),
  updated_at timestamptz not null default now()
);

-- Gacha banners (config data, not per-player state). Seeded in 0004_seed.sql.
-- Item ids are opaque to the server; the client maps them to characters/cards.
create table if not exists public.banners (
  id smallint primary key,
  name text not null,
  featured_five_star integer not null,
  standard_five_star integer[] not null,
  four_star integer[] not null,
  three_star integer[] not null,
  five_star_pity integer not null default 90,
  four_star_pity integer not null default 10,
  cost_per_pull integer not null default 160,
  daily_limit integer not null default 0,
  active boolean not null default true
);

-- Item catalog: every item id the server can grant. Seeded in 0004_seed.sql
-- with the same ids/names the HTML client already uses (ITEM_NAMES).
create table if not exists public.items (
  id integer primary key,
  name text not null,
  kind text not null,
  rarity smallint not null check (rarity between 1 and 5)
);

-- Per-account, per-banner gacha state (pity counters, 50/50 guarantee).
create table if not exists public.gacha_state (
  user_id uuid not null references auth.users (id) on delete cascade,
  banner_id smallint not null references public.banners (id),
  total integer not null default 0,
  since_five integer not null default 0,
  since_four integer not null default 0,
  featured_since integer not null default 0,
  guaranteed boolean not null default false,
  daily_count integer not null default 0,
  daily_anchor timestamptz not null default now(),
  primary key (user_id, banner_id)
);

-- Owned characters/cards/items. One row per (account, item); pulls and mail
-- claims increment count. Characters start at level 1.
create table if not exists public.inventory (
  user_id uuid not null references auth.users (id) on delete cascade,
  item_id integer not null references public.items (id),
  kind text not null,
  count integer not null default 0 check (count >= 0),
  level integer not null default 1,
  primary key (user_id, item_id)
);

-- Mail with optional attachments. Attachments are JSONB in the C# DTO shape:
-- [{itemId, kind, count}] where kind is "premium" | "free" | "ticket" for
-- currency, anything else for an inventory item.
create table if not exists public.mail (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references auth.users (id) on delete cascade,
  title text not null,
  body text not null default '',
  attachments jsonb not null default '[]'::jsonb,
  read boolean not null default false,
  claimed boolean not null default false,
  created_at timestamptz not null default now(),
  expires_at timestamptz
);

-- Cloud saves: one text blob per (account, slot), slots 1-3.
-- Stored as text (not jsonb) to match the C# prototype, which accepts any
-- save string from the client.
create table if not exists public.cloud_saves (
  user_id uuid not null references auth.users (id) on delete cascade,
  slot smallint not null check (slot between 1 and 3),
  data_json text not null,
  updated_at timestamptz not null default now(),
  primary key (user_id, slot)
);

-- Hot-path indexes.
create index if not exists mail_user_created_idx on public.mail (user_id, created_at desc);
create index if not exists inventory_user_idx on public.inventory (user_id);
create index if not exists gacha_state_user_idx on public.gacha_state (user_id);
create index if not exists cloud_saves_user_idx on public.cloud_saves (user_id);

-- Row Level Security on every table. Policies live in 0002_policies.sql.
alter table public.profiles enable row level security;
alter table public.wallets enable row level security;
alter table public.banners enable row level security;
alter table public.items enable row level security;
alter table public.gacha_state enable row level security;
alter table public.inventory enable row level security;
alter table public.mail enable row level security;
alter table public.cloud_saves enable row level security;
