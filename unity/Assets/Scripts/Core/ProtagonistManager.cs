// ProtagonistManager — dual-gender protagonist system (ASTRAL BLOOM).
// Static (no GameObject): the story reads ONE unified identity
// （无星者， element star) no matter which gender was chosen; the
// presentation layer reads the gender-matched resources (art / animation
// set) from the same config. There is exactly ONE story progression —
// gender never forks the plot, it only changes the presentation.
//
// Data source: Assets/StreamingAssets/protagonist-config.json (shipped
// with the build; the user's schema verbatim, plus art_resource / intro /
// trait per gender and a shared, data-only combat section).
//
// Persistence: the choice is saved in TWO places so a restart can never
// lose it — PlayerPrefs ("gm_protagonist_gender") and a local JSON save
// file (persistentDataPath/protagonist-save.json). PlayerPrefs wins on
// load; if it is empty, the save file is adopted (and PlayerPrefs
// restored from it). The shipped config file is never rewritten — it is
// read-only inside a build. The choice survives logout: it is a player
// identity, not a session token.
// Art: loaded by resource name from the config ("Characters/<art_resource>").
// The user generates the art themselves ("人物等我自己生"), so until the
// real art arrives the selection screen shows an explicit placeholder
// (立绘待补充）. Dropping protagonist_male.png / protagonist_female.png
// into Assets/Resources/Characters/ makes the real art appear with no
// code change.
using System;
using System.IO;
using UnityEngine;

namespace GalgameOnline.Core
{
    public static class ProtagonistManager
    {
        public const string GenderMale = "male";
        public const string GenderFemale = "female";

        /// <summary>Unified story identity — the same for both genders.
        /// The story system (StoryScreen, scripts) reads THIS, never gender.</summary>
        public const string StoryIdentity = "无星者";

        /// <summary>Shared element for both genders.</summary>
        public const string Element = "star";

        [Serializable]
        public class ProtagonistEntry
        {
            public string id;
            public string name;
            public string gender;
            public string title;
            public string element;
            public string story_identity;
            public string art_resource;
            public string intro;
            public string trait;
        }

        [Serializable]
        public class CombatSkill
        {
            public string id;
            public string name;
            public string type;
        }

        [Serializable]
        public class CombatStats
        {
            public int hp;
            public int attack;
            public int defense;
            public int speed;
        }

        [Serializable]
        public class CombatAwakening
        {
            public string id;
            public string name;
            public bool unlocked;
        }

        /// <summary>Shared combat config (DATA ONLY — no combat system exists
        /// yet). Identical for both genders: same element, role, stats,
        /// weapon, skills and awakening. Per-gender attack / idle animations
        /// are allowed later via AnimationSetForGender.</summary>
        [Serializable]
        public class CombatConfig
        {
            public bool data_only;
            public string note;
            public string element;
            public string role;
            public string weapon;
            public CombatStats base_stats;
            public CombatSkill[] skills;
            public CombatAwakening awakening;
        }

        [Serializable]
        public class ProtagonistConfig
        {
            public string selected_gender;
            public ProtagonistEntry male;
            public ProtagonistEntry female;
        }

        [Serializable]
        private class RootConfig
        {
            public ProtagonistConfig protagonist;
            public CombatConfig combat;
        }

        [Serializable]
        private class ProtagonistSave
        {
            public string selected_gender;
        }

        private const string PrefsGender = "gm_protagonist_gender";
        private const string SaveFileName = "protagonist-save.json";
        private const string ConfigFileName = "protagonist-config.json";

        private static RootConfig _config;
        private static bool _configTried;
        private static string _cachedGender; // null = not loaded yet
        private static bool _genderLoaded;

        private static RootConfig Config
        {
            get
            {
                if (_configTried) return _config;
                _configTried = true;
                _config = LoadConfigFile() ?? BuiltInConfig();
                return _config;
            }
        }

        private static RootConfig LoadConfigFile()
        {
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, ConfigFileName);
                if (!File.Exists(path)) return null;
                var cfg = JsonUtility.FromJson<RootConfig>(File.ReadAllText(path));
                if (cfg == null || cfg.protagonist == null || cfg.protagonist.male == null
                    || cfg.protagonist.female == null) return null;
                return cfg;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Protagonist] config load failed, using built-in defaults: " + e.Message);
                return null;
            }
        }

        // Built-in fallback (same values as the shipped config) so the
        // system works even if the config file is missing.
        private static RootConfig BuiltInConfig()
        {
            return new RootConfig
            {
                protagonist = new ProtagonistConfig
                {
                    selected_gender = null,
                    male = new ProtagonistEntry
                    {
                        id = "protagonist_male", name = "无星者", gender = GenderMale,
                        title = "无星者", element = Element, story_identity = StoryIdentity,
                        art_resource = "protagonist_male",
                        intro = "能够听见已经消失的星辰声音的少年。沉稳、理性，星律在他手中如星空般安静而深远。",
                        trait = "沉稳 · 理性 · 神秘",
                    },
                    female = new ProtagonistEntry
                    {
                        id = "protagonist_female", name = "无星者", gender = GenderFemale,
                        title = "无星者", element = Element, story_identity = StoryIdentity,
                        art_resource = "protagonist_female",
                        intro = "能够听见已经消失的星辰声音的少女。清冷、坚韧，星光在她身后轻盈流转。",
                        trait = "清冷 · 坚韧 · 灵动",
                    },
                },
                combat = new CombatConfig
                {
                    data_only = true,
                    note = "战斗系统尚未实现；以下为男女主角共享的配置骨架。",
                    element = Element, role = "protagonist", weapon = "星律武器（待定）",
                    base_stats = new CombatStats { hp = 100, attack = 20, defense = 10, speed = 10 },
                    skills = new[]
                    {
                        new CombatSkill { id = "normal_attack", name = "星律普攻（待定）", type = "normal" },
                        new CombatSkill { id = "skill", name = "星律技能（待定）", type = "active" },
                        new CombatSkill { id = "ultimate", name = "星律爆发（待定）", type = "ultimate" },
                    },
                    awakening = new CombatAwakening { id = "star_awakening", name = "星律觉醒（待定）", unlocked = false },
                },
            };
        }

        /// <summary>Selected gender: "male" / "female", or null if the
        /// player has never chosen. Reads PlayerPrefs first, then the local
        /// save file. Never throws.</summary>
        public static string SelectedGender
        {
            get
            {
                if (_genderLoaded) return _cachedGender;
                _genderLoaded = true;
                string g = PlayerPrefs.GetString(PrefsGender, "");
                if (g == GenderMale || g == GenderFemale)
                {
                    _cachedGender = g;
                    return _cachedGender;
                }
                // PlayerPrefs empty (e.g. cleared): adopt the save file.
                try
                {
                    string path = Path.Combine(Application.persistentDataPath, SaveFileName);
                    if (File.Exists(path))
                    {
                        var save = JsonUtility.FromJson<ProtagonistSave>(File.ReadAllText(path));
                        if (save != null && (save.selected_gender == GenderMale
                            || save.selected_gender == GenderFemale))
                        {
                            _cachedGender = save.selected_gender;
                            PlayerPrefs.SetString(PrefsGender, _cachedGender);
                            PlayerPrefs.Save();
                            return _cachedGender;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Protagonist] save-file read failed: " + e.Message);
                }
                _cachedGender = null;
                return null;
            }
        }

        public static bool HasSelection => SelectedGender != null;

        /// <summary>The config entry for the selected gender, or null.</summary>
        public static ProtagonistEntry Selected => SelectedGender == GenderMale
            ? Config.protagonist.male
            : SelectedGender == GenderFemale ? Config.protagonist.female : null;

        public static ProtagonistEntry Male => Config.protagonist.male;
        public static ProtagonistEntry Female => Config.protagonist.female;

        /// <summary>Shared combat config (data only). Same object for both
        /// genders — there is no per-gender combat data.</summary>
        public static CombatConfig Combat => Config.combat;

        /// <summary>Resource name for the selected gender's art
        /// ("Characters/&lt;art_resource&gt;"), or null if unselected.</summary>
        public static string SelectedArtResource => Selected?.art_resource;

        /// <summary>Short gender label for UI ("男" / "女"), "" if unselected.</summary>
        public static string SelectedGenderLabel => SelectedGender == GenderMale ? "男"
            : SelectedGender == GenderFemale ? "女" : "";

        /// <summary>Dialogue address adaptation: the story text uses one
        /// script; only the written pronoun changes with the chosen gender
        /// （他 / 她）. No second storyline exists.</summary>
        public static string SubjectPronoun => SelectedGender == GenderFemale ? "她" : "他";
        public static string PossessivePronoun => SelectedGender == GenderFemale ? "她的" : "他的";

        /// <summary>Per-gender animation set name (data-only hook for the
        /// future combat /演出 system). Same mechanics, different motion.</summary>
        public static string AnimationSetForGender(string gender)
            => gender == GenderFemale ? "protagonist_female_anims" : "protagonist_male_anims";

        /// <summary>Save the player's choice. Writes PlayerPrefs AND the
        /// local save file, then verifies by re-reading. Returns false only
        /// if the gender is invalid or both writes failed.</summary>
        public static bool SelectGender(string gender)
        {
            if (gender != GenderMale && gender != GenderFemale)
            {
                Debug.LogWarning("[Protagonist] SelectGender called with invalid gender: " + gender);
                return false;
            }
            bool ok = true;
            try
            {
                PlayerPrefs.SetString(PrefsGender, gender);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Protagonist] PlayerPrefs save failed: " + e.Message);
                ok = false;
            }
            try
            {
                string path = Path.Combine(Application.persistentDataPath, SaveFileName);
                File.WriteAllText(path, JsonUtility.ToJson(
                    new ProtagonistSave { selected_gender = gender }));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Protagonist] save-file write failed: " + e.Message);
                ok = false;
            }
            _cachedGender = gender;
            _genderLoaded = true;
            // Verify: the value must read back.
            if (PlayerPrefs.GetString(PrefsGender, "") != gender)
            {
                Debug.LogWarning("[Protagonist] verification read-back mismatch after save.");
                ok = false;
            }
            Debug.Log("[Protagonist] selection saved: " + gender + " (verified=" + ok + ")");
            return ok;
        }

        /// <summary>Test-only: forget the cached value so the next read
        /// goes to disk again (used by the smoke persistence check).</summary>
        public static void InvalidateCache()
        {
            _genderLoaded = false;
            _cachedGender = null;
        }
    }
}
