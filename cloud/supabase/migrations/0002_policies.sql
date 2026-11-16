-- GalgameOnline — 0002_policies
-- RLS: the authenticated role can SELECT its own rows (and the public
-- catalogs). There are deliberately NO insert/update/delete policies for
-- authenticated on any game-state table: a client that talks to Supabase
-- directly cannot write wallets, pity counters, inventory, mail, or saves.
-- All writes go through the security-definer RPC functions in
-- 0003_functions.sql, called by the Worker with the service_role key
-- (service_role bypasses RLS).
-- The anon role gets no policies at all (deny everything).

-- Profiles: a user reads only their own profile.
create policy "profiles_select_own"
  on public.profiles for select
  to authenticated
  using (id = auth.uid());

-- Wallets: own balance only.
create policy "wallets_select_own"
  on public.wallets for select
  to authenticated
  using (user_id = auth.uid());

-- Gacha state: own pity counters only.
create policy "gacha_state_select_own"
  on public.gacha_state for select
  to authenticated
  using (user_id = auth.uid());

-- Inventory: own items only.
create policy "inventory_select_own"
  on public.inventory for select
  to authenticated
  using (user_id = auth.uid());

-- Mail: own mail only.
create policy "mail_select_own"
  on public.mail for select
  to authenticated
  using (user_id = auth.uid());

-- Cloud saves: own saves only.
create policy "cloud_saves_select_own"
  on public.cloud_saves for select
  to authenticated
  using (user_id = auth.uid());

-- Public catalogs: any signed-in user may read banners and items.
create policy "banners_select_all"
  on public.banners for select
  to authenticated
  using (true);

create policy "items_select_all"
  on public.items for select
  to authenticated
  using (true);
