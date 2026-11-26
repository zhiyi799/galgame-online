using UnityEngine;

namespace TideSong
{
    /// <summary>进度存档：位置、等级、结晶、锚点与宝箱状态。</summary>
    public static class SaveSystem
    {
        const string Key = "TideSong.Save.v1";

        [System.Serializable]
        class Data
        {
            public bool has;
            public float px, py, pz;
            public int level, exp, crystals, chests;
            public string shrines = "";
            public bool altarDone;
        }

        public static void SaveFrom(GameManager gm)
        {
            if (gm == null || gm.Player == null) return;
            var d = new Data
            {
                has = true,
                px = gm.Player.transform.position.x,
                py = gm.Player.transform.position.y,
                pz = gm.Player.transform.position.z,
                level = gm.Player.Level,
                exp = gm.Player.Exp,
                crystals = gm.Player.CrystalCount,
                chests = gm.Player.ChestOpened
            };

            var sb = new System.Text.StringBuilder();
            foreach (var it in gm.Interactables)
            {
                if (it is TideShrine) { var s = (TideShrine)it; if (s.activated) sb.Append(s.index).Append(','); }
                if (it is TideAltar) { var a = (TideAltar)it; if (a.completed) d.altarDone = true; }
            }
            d.shrines = sb.ToString();

            PlayerPrefs.SetString(Key, JsonUtility.ToJson(d));
            PlayerPrefs.Save();

            if (TeamManager.Instance != null) TeamManager.Instance.Save();
            if (EchoSystem.Instance != null) EchoSystem.Instance.Save();

            GameHUD.Instance?.Toast("进度已保存");
        }

        public static void LoadInto(GameManager gm)
        {
            if (gm == null || gm.Player == null) return;
            if (!PlayerPrefs.HasKey(Key)) return;
            Data d = null;
            try { d = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Key)); } catch { }
            if (d == null || !d.has) return;

            gm.Player.Level = Mathf.Max(1, d.level);
            gm.Player.Exp = d.exp;
            gm.Player.CrystalCount = d.crystals;
            gm.Player.ChestOpened = d.chests;

            var pos = new Vector3(d.px, d.py, d.pz);
            if (pos.y < WorldData.HeightAt(pos.x, pos.z) - 2f) pos.y = WorldData.HeightAt(pos.x, pos.z) + 1.5f;
            gm.Player.Teleport(pos);

            // 恢复锚点
            if (!string.IsNullOrEmpty(d.shrines))
            {
                var parts = d.shrines.Split(',');
                foreach (var it in gm.Interactables)
                {
                    var s = it as TideShrine;
                    if (s == null) continue;
                    foreach (var p in parts)
                    {
                        int idx;
                        if (int.TryParse(p, out idx) && idx == s.index)
                        {
                            s.activated = true;
                            gm.Player.ShrinesLit++;
                            if (s.glow != null) s.glow.intensity = 4.5f;
                        }
                    }
                }
            }

            // 声骸先加载（影响生命上限），再恢复队伍状态
            if (EchoSystem.Instance != null) EchoSystem.Instance.Load();
            if (TeamManager.Instance != null) TeamManager.Instance.Load();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey("TideSong.Team.v1");
            PlayerPrefs.DeleteKey("TideSong.Echo.v1");
            PlayerPrefs.Save();
        }
    }
}
