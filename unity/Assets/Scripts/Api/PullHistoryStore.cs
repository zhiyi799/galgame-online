// PullHistoryStore — local pull-history persistence.
//
// LOCAL-ONLY: the server has NO pull-history endpoint and no pull_history
// table, so history lives only in this per-account JSON file on the device.
// It is LOST on device change or app-data clear, and it cannot be recovered
// from the server. Logout does NOT delete history files (one file per
// account, so switching accounts never mixes histories).
// Callers use GameManager.Username as the username.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GalgameOnline.Api
{
    [Serializable]
    public class PullRecord
    {
        /// <summary>Per-file auto-increment sequence number (assigned by Append).</summary>
        public int seq;
        /// <summary>UTC ISO-8601 time the pull result was received (set at record time).</summary>
        public string time;
        public int bannerId;
        /// <summary>Banner name, stored redundantly so history stays readable if a banner is renamed.</summary>
        public string bannerName;
        public int itemId;
        /// <summary>Rarity: 3, 4 or 5.</summary>
        public int rarity;
        public bool isFeatured;
        /// <summary>"character" or "item".</summary>
        public string kind;
        /// <summary>Position within that single/ten pull: 1-10.</summary>
        public int pullNo;
    }

    [Serializable]
    public class PullHistoryFile
    {
        public int version = 1;
        public string account;
        public List<PullRecord> records;
    }

    public static class PullHistoryStore
    {
        /// <summary>Only the most recent this many records are kept; older ones are dropped on Append.</summary>
        public const int MaxRecords = 500;

        private static string SanitizeUsername(string username)
        {
            if (string.IsNullOrEmpty(username)) return "guest";
            char[] invalid = Path.GetInvalidFileNameChars();
            char[] chars = username.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0) chars[i] = '_';
            }
            string safe = new string(chars).Trim();
            return string.IsNullOrEmpty(safe) ? "guest" : safe;
        }

        /// <summary>Per-account history file path: persistentDataPath/pull_history_&lt;username&gt;.json</summary>
        public static string GetFilePath(string username)
        {
            return Path.Combine(Application.persistentDataPath, "pull_history_" + SanitizeUsername(username) + ".json");
        }

        /// <summary>Loads the account's history. Returns an empty list (never null) if the file does not exist or is corrupt (logs a warning on corrupt data, does not throw).</summary>
        public static List<PullRecord> Load(string username)
        {
            string path = GetFilePath(username);
            try
            {
                if (!File.Exists(path)) return new List<PullRecord>();
                string json = File.ReadAllText(path);
                PullHistoryFile file = JsonUtility.FromJson<PullHistoryFile>(json);
                if (file == null || file.records == null) return new List<PullRecord>();
                return file.records;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PullHistoryStore] Failed to load pull history from " + path + ": " + e.Message);
                return new List<PullRecord>();
            }
        }

        /// <summary>Appends records: assigns seq continuing from the max existing seq (and fills in time if unset), keeps only the most recent 500 records, and saves synchronously. Never throws on IO failure — logs and returns, but those records are then lost (the server cannot restore them); the caller should Toast about it.</summary>
        public static void Append(string username, List<PullRecord> newRecords)
        {
            if (newRecords == null || newRecords.Count == 0) return;
            try
            {
                List<PullRecord> records = Load(username);
                int maxSeq = 0;
                foreach (PullRecord r in records)
                {
                    if (r != null && r.seq > maxSeq) maxSeq = r.seq;
                }
                foreach (PullRecord r in newRecords)
                {
                    if (r == null) continue;
                    maxSeq++;
                    r.seq = maxSeq;
                    if (string.IsNullOrEmpty(r.time))
                        r.time = DateTime.UtcNow.ToString("o");
                    records.Add(r);
                }
                if (records.Count > MaxRecords)
                    records.RemoveRange(0, records.Count - MaxRecords);
                Save(username, records);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PullHistoryStore] Failed to append pull history: " + e.Message);
            }
        }

        /// <summary>Writes the whole record list for the account (used by Append internally). Never throws on IO failure — logs and returns.</summary>
        public static void Save(string username, List<PullRecord> records)
        {
            try
            {
                var file = new PullHistoryFile
                {
                    version = 1,
                    account = username,
                    records = records ?? new List<PullRecord>()
                };
                File.WriteAllText(GetFilePath(username), JsonUtility.ToJson(file, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PullHistoryStore] Failed to save pull history: " + e.Message);
            }
        }
    }
}
