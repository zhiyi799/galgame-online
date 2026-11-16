-- GalgameOnline — 0004_seed
-- Banner catalog + item catalog, copied from the C# prototype's
-- BannerCatalog (GameServer/Services/Banners.cs) and the HTML client's
-- ITEM_NAMES table. Item ids are opaque to the server; the client maps them
-- to characters/cards. 5-star/4-star entries are kind 'character', 3-star
-- entries are kind 'item', matching what the gacha engine grants.
-- Names are placeholders — rename them here (and in the client's ITEM_NAMES)
-- when the real characters/cards are designed.

insert into public.banners
  (id, name, featured_five_star, standard_five_star, four_star, three_star,
   five_star_pity, four_star_pity, cost_per_pull, daily_limit, active)
values
  (1, '星夜邂逅', 101, array[102, 103, 104], array[201, 202, 203, 204],
   array[301, 302, 303], 90, 10, 160, 0, true),
  (2, '月下初见', 105, array[102, 103, 104], array[201, 202, 203, 205],
   array[301, 302, 304], 90, 10, 160, 0, true)
on conflict (id) do update set
  name = excluded.name,
  featured_five_star = excluded.featured_five_star,
  standard_five_star = excluded.standard_five_star,
  four_star = excluded.four_star,
  three_star = excluded.three_star,
  five_star_pity = excluded.five_star_pity,
  four_star_pity = excluded.four_star_pity,
  cost_per_pull = excluded.cost_per_pull,
  daily_limit = excluded.daily_limit,
  active = excluded.active;

insert into public.items (id, name, kind, rarity) values
  (101, '星夜·凛', 'character', 5),
  (102, '星夜·雪', 'character', 5),
  (103, '星夜·花', 'character', 5),
  (104, '星夜·月', 'character', 5),
  (105, '月下·凛', 'character', 5),
  (201, '四星·凛', 'character', 4),
  (202, '四星·雪', 'character', 4),
  (203, '四星·花', 'character', 4),
  (204, '四星·祭', 'character', 4),
  (205, '四星·夜', 'character', 4),
  (301, '三星·信物', 'item', 3),
  (302, '三星·花瓣', 'item', 3),
  (303, '三星·纸鹤', 'item', 3),
  (304, '三星·月光', 'item', 3)
on conflict (id) do update set
  name = excluded.name,
  kind = excluded.kind,
  rarity = excluded.rarity;
