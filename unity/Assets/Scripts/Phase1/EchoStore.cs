// EchoStore — 星律回响本地存档（Phase 1 / P1-B）。
// 本地 JSON（Application.persistentDataPath/astralbloom_echoes.json），
// 先本地，云端 Phase 2。星律板（工坊产物）存在同一份文件里，供
// P1-C 工坊与 P1-B 回响强化共用。
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AstralBloom
{
    [Serializable]
    public class EchoInstance
    {
        public int uid;
        public int defId;
        public int level = 1;
    }

    [Serializable]
    public class WorkshopFacilitySave
    {
        public int type; // 0 采集器，1 输送带，2 加工站，3 仓库
        public float x;
        public float z;
        public float rotY;
    }

    [Serializable]
    public class EchoSaveData
    {
        public List<EchoInstance> inventory = new List<EchoInstance>();
        // 4 名队员 × 2 槽，存回响 uid，未装备为 -1。
        public int[] equipped = new int[8] { -1, -1, -1, -1, -1, -1, -1, -1 };
        public int starPlates;
        public long lastSeenTicks;
        public int nextUid = 1;
        // 工坊（P1-C）：仓库内存放的星律板 + 已放置设施（空则建标准线）。
        public int warehousePlates;
        public List<WorkshopFacilitySave> facilities = new List<WorkshopFacilitySave>();
    }

    public static class EchoStore
    {
        // 进程内唯一一份存档（回响与工坊共用，防两份内存拷贝互相覆盖）。
        private static EchoSaveData _current;

        public static EchoSaveData Current
        {
            get
            {
                if (_current == null) _current = Load();
                return _current;
            }
        }

        public static void Save() { Save(Current); }

        public static string FilePath
        {
            get { return Path.Combine(Application.persistentDataPath, "astralbloom_echoes.json"); }
        }

        public static EchoSaveData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var data = JsonUtility.FromJson<EchoSaveData>(File.ReadAllText(FilePath));
                    if (data != null)
                    {
                        if (data.inventory == null) data.inventory = new List<EchoInstance>();
                        if (data.equipped == null || data.equipped.Length != 8)
                            data.equipped = new int[8] { -1, -1, -1, -1, -1, -1, -1, -1 };
                        if (data.nextUid < 1) data.nextUid = 1;
                        if (data.facilities == null) data.facilities = new List<WorkshopFacilitySave>();
                        return data;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Echo] 存档读取失败，使用空存档：" + e.Message);
            }
            return new EchoSaveData();
        }

        public static void Save(EchoSaveData data)
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Echo] 存档写入失败：" + e.Message);
            }
        }
    }
}
