// TeamStore — local team formation (up to 3 owned characters).
// LOCAL-ONLY: the server has no team endpoint, so the team lives in
// PlayerPrefs (same idea as ProtagonistManager's double save, but a team
// is small enough for one key). Comma-separated item IDs, e.g. "101,201".
// Never invents characters: only item IDs the player actually owns can
// be saved (callers must check ownership before calling Toggle/Save).
using System.Collections.Generic;
using UnityEngine;

namespace GalgameOnline.Api
{
    public static class TeamStore
    {
        public const int MaxTeamSize = 3;
        private const string PrefsKey = "gm_team";

        public static List<int> Load()
        {
            var team = new List<int>();
            string raw = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(raw)) return team;
            foreach (var part in raw.Split(','))
            {
                if (int.TryParse(part.Trim(), out int id) && id > 0 && !team.Contains(id))
                    team.Add(id);
            }
            if (team.Count > MaxTeamSize) team.RemoveRange(MaxTeamSize, team.Count - MaxTeamSize);
            return team;
        }

        public static void Save(List<int> team)
        {
            if (team == null) team = new List<int>();
            var clean = new List<int>();
            foreach (int id in team)
            {
                if (id > 0 && !clean.Contains(id) && clean.Count < MaxTeamSize)
                    clean.Add(id);
            }
            PlayerPrefs.SetString(PrefsKey, string.Join(",", clean.ConvertAll(i => i.ToString()).ToArray()));
            PlayerPrefs.Save();
        }

        public static bool Contains(int itemId) => Load().Contains(itemId);

        /// <summary>Adds the character if absent, removes it if present.
        /// Returns false (and does nothing) when the team is already full.</summary>
        public static bool Toggle(int itemId)
        {
            var team = Load();
            if (team.Contains(itemId))
            {
                team.Remove(itemId);
                Save(team);
                return true;
            }
            if (team.Count >= MaxTeamSize) return false;
            team.Add(itemId);
            Save(team);
            return true;
        }
    }
}
