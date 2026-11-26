// WorkshopManager — 星律工坊 v1（Phase 1 / P1-C，Phase 2 空间物流）。
// 位置：WorldMap 苍穹区域工坊用地，中心 (2960, 1100, -315)，70×40 m，
// 地块已验证平坦（70×40 m 内高差 0.81 m）；设施运行时按地形高度落位。
// 生产线：采集器（星律矿脉）→ 输送带 → 加工站（星律矿 → 星律板）→ 仓库。
// 空间物流（Phase 2，源自灰域协议工厂逻辑，C# 重写）：每条输送带有方向
//       （取设施本地 +X，即 rotY=0 → +X，90 → -Z，180 → -X，270 → +Z），
//       物品沿方向流动；输送带每条最多 3 件，单件过带 2 秒；加工站输入
//       缓冲上限 5；推不出去时背压停滞（不丢货）。旋转 R 直接改变出货方向。
// 电力：能源核心供能 100，电力塔每座 +25 容量；采集器 20、输送带 5、
//       加工站 40、仓库 0、电力塔 0。功耗 > 容量 → 电力不足，全线降速
//       至 45%（不再整条线停机）。
// 生产（在线）：采集器 1 矿/3s → 输送带（单件 2s 过带）→ 加工站输入
//       （上限 5）→ 加工站 1 矿 → 1 板/6s → 仓库（上限 50）。
// 离线结算：进场时按离线时长补发，6 板/小时（电力不足时按 45% 折算），
//       上限 8 小时，且不超过仓库空余容量；lastSeenTicks 每 60 s 更新一次
//       （仅工坊在场时）。
// 建造模式：B 进入/退出俯视建造；数字键 1–5 选设施类型，鼠标点地放置
//       （吸附 5 m 网格，限于用地内）；点设施选中，R 旋转 90°，X 拆除。
// 收取：WorkshopHud 的「收取星律板」把仓库的板转入星律板（回响强化用）。
// 设施与仓库存入 EchoStore（与回响共用一份本地 JSON）；矿石/加工缓冲
//       不存档，进场时从空开始。工坊用地标记运行时生成，不重新生成
//       WorldMap 场景（场景已验证，不动它）。
using System.Collections.Generic;
using UnityEngine;
using GalgameOnline.Core;
using GalgameOnline.WorldMap;

namespace AstralBloom
{
    public class WorkshopManager : MonoBehaviour
    {
        public static WorkshopManager Instance { get; private set; }

        public const int TypeMiner = 0;
        public const int TypeBelt = 1;
        public const int TypeProcessor = 2;
        public const int TypeWarehouse = 3;
        public const int TypePylon = 4;

        public const int PowerCore = 100;
        public const int PylonPower = 25;
        public const int WarehouseCap = 50;
        public const int BeltItemCap = 3;
        public const int ProcInputCap = 5;
        public const int MinerOutCap = 3;
        public const int ProcOutCap = 3;
        public const float OfflinePlatesPerHour = 6f;
        public const float OfflineMaxHours = 8f;
        public const float UnderpowerSpeed = 0.45f;
        public const float BeltTransitTime = 2f;

        public static readonly Vector3 PlotCenter = new Vector3(2960f, 1100f, -315f);
        public const float PlotHalfX = 35f;
        public const float PlotHalfZ = 20f;

        // 输送带上的在途物品（kind："ore" 矿石 / "plate" 星律板）。
        public class BeltItem
        {
            public string kind;
            public float t; // 沿带进度 0..1
        }

        public class Facility
        {
            public int type;
            public GameObject root;
            public Vector3 pos;
            public float rotY;
            public GameObject highlight;
            // 空间物流状态（Phase 2）：
            // ore：采集器 = 待推出矿石；加工站 = 输入缓冲矿石。
            // plates：加工站 = 待推出星律板。
            // timer：采集器 = 生产计时；加工站 = 加工计时。
            public int ore;
            public int plates;
            public float timer;
            // 输送带：在途物品 + 对应可视化小方块。
            public List<BeltItem> items;
            public List<GameObject> itemMeshes;
        }

        public static string TypeName(int type)
        {
            switch (type)
            {
                case TypeMiner: return "采集器";
                case TypeBelt: return "输送带";
                case TypeProcessor: return "加工站";
                case TypeWarehouse: return "仓库";
                case TypePylon: return "电力塔";
                default: return "未知";
            }
        }

        public static int PowerFor(int type)
        {
            switch (type)
            {
                case TypeMiner: return 20;
                case TypeBelt: return 5;
                case TypeProcessor: return 40;
                default: return 0;
            }
        }

        // HUD 显示用：电力塔提供容量，显示为 +25。
        public static string PowerText(int type)
        {
            if (type == TypePylon) return "+" + PylonPower;
            return PowerFor(type).ToString();
        }

        public List<Facility> Facilities { get; private set; }
        public bool BuildMode { get; private set; }
        public Facility Selected { get; private set; }
        public int GhostType { get; private set; }
        public Vector3 GhostPos { get; private set; }
        public bool GhostValid { get; private set; }

        public float BeltOre
        {
            get
            {
                // 输送带上在途矿石总数（聚合）。
                int n = 0;
                if (Facilities != null)
                    foreach (var f in Facilities)
                        if (f.type == TypeBelt && f.items != null)
                            foreach (var it in f.items) if (it.kind == "ore") n++;
                return n;
            }
        }

        public float ProcOre
        {
            get
            {
                // 各加工站输入缓冲矿石总数（聚合）。
                int n = 0;
                if (Facilities != null)
                    foreach (var f in Facilities)
                        if (f.type == TypeProcessor) n += f.ore;
                return n;
            }
        }

        public int BeltOreCap { get { return BeltItemCap * BeltCount; } }
        public int ProcOreCap { get { return ProcInputCap * ProcessorCount; } }

        public int WarehousePlates { get { return _data != null ? _data.warehousePlates : 0; } }
        public int StarPlates { get { return _data != null ? _data.starPlates : 0; } }

        private EchoSaveData _data;
        private WorldMapPlayer _player;
        private float _saveT;
        private GameObject _ghost;

        public int PowerUsed
        {
            get
            {
                int p = 0;
                if (Facilities != null)
                    foreach (var f in Facilities) p += PowerFor(f.type);
                return p;
            }
        }

        public int PylonCount { get { return CountOf(TypePylon); } }

        // 总容量 = 能源核心 100 + 每座电力塔 25。
        public int PowerCapacity { get { return PowerCore + PylonPower * PylonCount; } }

        public bool Overloaded { get { return PowerUsed > PowerCapacity; } }

        // 电力不足时全线降速至 45%（灰域协议模型），不再停机。
        public float SpeedFactor { get { return Overloaded ? UnderpowerSpeed : 1f; } }

        public bool LineComplete
        {
            get
            {
                bool miner = false, belt = false, proc = false, wh = false;
                if (Facilities != null)
                    foreach (var f in Facilities)
                    {
                        if (f.type == TypeMiner) miner = true;
                        else if (f.type == TypeBelt) belt = true;
                        else if (f.type == TypeProcessor) proc = true;
                        else if (f.type == TypeWarehouse) wh = true;
                    }
                return miner && belt && proc && wh;
            }
        }

        public bool Running { get { return LineComplete; } }

        public int MinerCount { get { return CountOf(TypeMiner); } }
        public int BeltCount { get { return CountOf(TypeBelt); } }
        public int ProcessorCount { get { return CountOf(TypeProcessor); } }

        private int CountOf(int type)
        {
            int n = 0;
            if (Facilities != null)
                foreach (var f in Facilities) if (f.type == type) n++;
            return n;
        }

        public void Init(WorldMapPlayer player)
        {
            Instance = this;
            _player = player;
            _data = EchoStore.Current;
            Facilities = new List<Facility>();
            BuildPlotMarker();
            LoadFacilities();
            SettleOffline();
            gameObject.AddComponent<WorkshopHud>();
            Debug.Log("[Workshop] 星律工坊就绪：设施 " + Facilities.Count +
                " 个，功耗 " + PowerUsed + "/" + PowerCapacity +
                "，仓库星律板 ×" + WarehousePlates);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            TouchLastSeen();
        }

        // ---------------- 建造与存档 ----------------

        private void LoadFacilities()
        {
            if (_data.facilities == null || _data.facilities.Count == 0)
            {
                PlaceStandardLine();
                return;
            }
            foreach (var s in _data.facilities)
            {
                var f = CreateFacility(s.type, new Vector3(s.x, 0f, s.z), s.rotY);
                if (f != null) Facilities.Add(f);
            }
        }

        private void PlaceStandardLine()
        {
            // 标准线（z=-323，x 在已验证平坦区 2938–2977 内）
            AddFacility(TypeMiner, new Vector3(2942f, 0f, -323f), 0f);
            AddFacility(TypeBelt, new Vector3(2952f, 0f, -323f), 0f);
            AddFacility(TypeProcessor, new Vector3(2962f, 0f, -323f), 0f);
            AddFacility(TypeWarehouse, new Vector3(2974f, 0f, -323f), 0f);
        }

        public Facility AddFacility(int type, Vector3 pos, float rotY)
        {
            var f = CreateFacility(type, pos, rotY);
            if (f == null) return null;
            Facilities.Add(f);
            SaveFacilities();
            return f;
        }

        public bool RemoveFacility(Facility f)
        {
            if (f == null || !Facilities.Contains(f)) return false;
            Facilities.Remove(f);
            if (Selected == f) Selected = null;
            DestroyItemMeshes(f);
            if (f.root != null) Destroy(f.root);
            SaveFacilities();
            Debug.Log("[Workshop] 拆除 " + TypeName(f.type) + "（功耗现 " + PowerUsed + "/" + PowerCapacity + "）");
            return true;
        }

        public void RotateSelected()
        {
            if (Selected == null) return;
            Selected.rotY = (Selected.rotY + 90f) % 360f;
            Selected.root.transform.rotation = Quaternion.Euler(0f, Selected.rotY, 0f);
            SaveFacilities();
        }

        private void SaveFacilities()
        {
            _data.facilities.Clear();
            foreach (var f in Facilities)
            {
                _data.facilities.Add(new WorkshopFacilitySave
                {
                    type = f.type,
                    x = f.pos.x,
                    z = f.pos.z,
                    rotY = f.rotY
                });
            }
            EchoStore.Save();
        }

        private Facility CreateFacility(int type, Vector3 pos, float rotY)
        {
            float ground = WorldMapTerrain.SampleHeight(pos.x, pos.z);
            var root = new GameObject("Workshop_" + TypeName(type));
            root.transform.position = new Vector3(pos.x, ground, pos.z);
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            BuildVisual(root.transform, type);
            var hl = new GameObject("Highlight");
            hl.transform.SetParent(root.transform, false);
            hl.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var hlMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hlMesh.transform.SetParent(hl.transform, false);
            hlMesh.transform.localPosition = Vector3.zero;
            hlMesh.transform.localScale = FacilitySize(type) + new Vector3(0.6f, 0.6f, 0.6f);
            var hlRend = hlMesh.GetComponent<Renderer>();
            hlRend.material = TideSong.MaterialLibrary.MakeTransparent(new Color(0.85f, 0.80f, 0.67f, 0.25f), 0f, 0.5f);
            Object.Destroy(hlMesh.GetComponent<BoxCollider>());
            hl.SetActive(false);
            var fac = new Facility
            {
                type = type,
                root = root,
                pos = new Vector3(pos.x, ground, pos.z),
                rotY = rotY,
                highlight = hl,
                ore = 0,
                plates = 0,
                timer = 0f,
                items = type == TypeBelt ? new List<BeltItem>() : null,
                itemMeshes = type == TypeBelt ? new List<GameObject>() : null
            };
            return fac;
        }

        private void DestroyItemMeshes(Facility f)
        {
            if (f == null || f.itemMeshes == null) return;
            foreach (var m in f.itemMeshes) if (m != null) Destroy(m);
            f.itemMeshes.Clear();
        }

        private static Vector3 FacilitySize(int type)
        {
            switch (type)
            {
                case TypeMiner: return new Vector3(4f, 2.5f, 4f);
                case TypeBelt: return new Vector3(12f, 0.6f, 2f);
                case TypeProcessor: return new Vector3(5f, 3f, 4f);
                case TypeWarehouse: return new Vector3(6f, 3.5f, 5f);
                case TypePylon: return new Vector3(2f, 5.5f, 2f);
                default: return new Vector3(4f, 2f, 4f);
            }
        }

        private void BuildVisual(Transform parent, int type)
        {
            switch (type)
            {
                case TypeMiner:
                    AddBox(parent, new Vector3(0f, 0.5f, 0f), new Vector3(4f, 1f, 4f),
                        new Color(0.25f, 0.26f, 0.30f));
                    AddBox(parent, new Vector3(0f, 1.6f, 0f), new Vector3(0.8f, 2.2f, 0.8f),
                        new Color(0.45f, 0.47f, 0.52f));
                    // 星律矿晶（青色）
                    var crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    crystal.transform.SetParent(parent, false);
                    crystal.transform.localPosition = new Vector3(1.2f, 1.4f, 1.2f);
                    crystal.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    crystal.transform.localScale = new Vector3(0.9f, 1.8f, 0.9f);
                    crystal.GetComponent<Renderer>().material =
                        TideSong.MaterialLibrary.Make(new Color(0.35f, 0.85f, 1f), 0.1f, 0.6f);
                    break;
                case TypeBelt:
                    AddBox(parent, new Vector3(0f, 0.3f, 0f), new Vector3(12f, 0.6f, 2f),
                        new Color(0.20f, 0.21f, 0.25f));
                    AddBox(parent, new Vector3(0f, 0.75f, -0.9f), new Vector3(12f, 0.3f, 0.2f),
                        new Color(0.45f, 0.47f, 0.52f));
                    AddBox(parent, new Vector3(0f, 0.75f, 0.9f), new Vector3(12f, 0.3f, 0.2f),
                        new Color(0.45f, 0.47f, 0.52f));
                    break;
                case TypeProcessor:
                    AddBox(parent, new Vector3(0f, 1.5f, 0f), new Vector3(5f, 3f, 4f),
                        new Color(0.25f, 0.45f, 0.75f));
                    var chimney = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    chimney.transform.SetParent(parent, false);
                    chimney.transform.localPosition = new Vector3(-1.5f, 4f, -1f);
                    chimney.transform.localScale = new Vector3(0.8f, 2f, 0.8f);
                    chimney.GetComponent<Renderer>().material =
                        TideSong.MaterialLibrary.Make(new Color(0.55f, 0.58f, 0.62f), 0.6f, 0.4f);
                    AddBox(parent, new Vector3(1.2f, 1.6f, 2.05f), new Vector3(2f, 1f, 0.1f),
                        new Color(0.35f, 0.85f, 1f));
                    break;
                case TypeWarehouse:
                    AddBox(parent, new Vector3(0f, 1.75f, 0f), new Vector3(6f, 3.5f, 5f),
                        new Color(0.45f, 0.38f, 0.30f));
                    AddBox(parent, new Vector3(0f, 3.8f, 0f), new Vector3(6.6f, 0.5f, 5.6f),
                        new Color(0.30f, 0.26f, 0.22f));
                    AddBox(parent, new Vector3(0f, 1.2f, 2.55f), new Vector3(2.4f, 2.4f, 0.1f),
                        new Color(0.20f, 0.18f, 0.16f));
                    break;
                case TypePylon:
                    // 电力塔：细杆 + 顶部发光球（与工坊能源核心小塔同风格）。
                    var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    stem.transform.SetParent(parent, false);
                    stem.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                    stem.transform.localScale = new Vector3(0.5f, 4.4f, 0.5f);
                    stem.GetComponent<Renderer>().material =
                        TideSong.MaterialLibrary.Make(new Color(0.45f, 0.50f, 0.58f), 0.6f, 0.4f);
                    var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    orb.transform.SetParent(parent, false);
                    orb.transform.localPosition = new Vector3(0f, 4.8f, 0f);
                    orb.transform.localScale = Vector3.one * 0.9f;
                    orb.GetComponent<Renderer>().material =
                        TideSong.MaterialLibrary.Make(new Color(0.35f, 0.85f, 1f), 0.1f, 0.8f);
                    var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    ring.transform.SetParent(parent, false);
                    ring.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                    ring.transform.localScale = new Vector3(1.6f, 0.4f, 1.6f);
                    ring.GetComponent<Renderer>().material =
                        TideSong.MaterialLibrary.Make(new Color(0.25f, 0.26f, 0.30f), 0.6f, 0.4f);
                    break;
            }
        }

        private static void AddBox(Transform parent, Vector3 localPos, Vector3 size, Color color)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPos;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().material =
                TideSong.MaterialLibrary.Make(color, 0.2f, 0.35f);
        }

        private void BuildPlotMarker()
        {
            // 用地地面标识（半透明）+ 四角立柱 + 能源核心小塔
            var ground = new GameObject("WorkshopPlot");
            ground.transform.position = new Vector3(PlotCenter.x,
                WorldMapTerrain.SampleHeight(PlotCenter.x, PlotCenter.z) + 0.15f, PlotCenter.z);
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.transform.SetParent(ground.transform, false);
            plane.transform.localScale = new Vector3(7f, 1f, 4f);
            plane.GetComponent<Renderer>().material =
                TideSong.MaterialLibrary.MakeTransparent(new Color(0.55f, 0.75f, 1f, 0.10f), 0f, 0.5f);
            Object.Destroy(plane.GetComponent<MeshCollider>());

            Vector3[] corners = {
                new Vector3(-PlotHalfX, 0f, -PlotHalfZ),
                new Vector3(PlotHalfX, 0f, -PlotHalfZ),
                new Vector3(-PlotHalfX, 0f, PlotHalfZ),
                new Vector3(PlotHalfX, 0f, PlotHalfZ),
            };
            foreach (var c in corners)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.transform.SetParent(ground.transform, false);
                post.transform.localPosition = new Vector3(c.x, 2f, c.z);
                post.transform.localScale = new Vector3(0.5f, 4f, 0.5f);
                post.GetComponent<Renderer>().material =
                    TideSong.MaterialLibrary.Make(new Color(0.85f, 0.80f, 0.67f), 0.4f, 0.5f);
            }

            // 能源核心小塔（供能 100 的视觉锚点）
            var pylon = new GameObject("WorkshopPowerCore");
            pylon.transform.position = new Vector3(PlotCenter.x,
                WorldMapTerrain.SampleHeight(PlotCenter.x, PlotCenter.z + 10f), PlotCenter.z + 10f);
            var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.transform.SetParent(pylon.transform, false);
            stem.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            stem.transform.localScale = new Vector3(1.2f, 5f, 1.2f);
            stem.GetComponent<Renderer>().material =
                TideSong.MaterialLibrary.Make(new Color(0.45f, 0.50f, 0.58f), 0.6f, 0.4f);
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.transform.SetParent(pylon.transform, false);
            orb.transform.localPosition = new Vector3(0f, 5.8f, 0f);
            orb.transform.localScale = Vector3.one * 1.6f;
            orb.GetComponent<Renderer>().material =
                TideSong.MaterialLibrary.Make(new Color(0.35f, 0.85f, 1f), 0.1f, 0.8f);
        }

        // ---------------- 离线结算 ----------------

        public int SettleOffline()
        {
            if (_data.lastSeenTicks <= 0) { TouchLastSeen(); return 0; }
            var last = new System.DateTime(_data.lastSeenTicks, System.DateTimeKind.Utc);
            double hours = (System.DateTime.UtcNow - last).TotalHours;
            if (hours < 0.05) { TouchLastSeen(); return 0; } // 3 分钟内不结算
            hours = System.Math.Min(hours, OfflineMaxHours);
            int granted = 0;
            if (LineComplete)
            {
                // 电力不足时离线产出按 45% 折算（与在线降速一致）。
                float rate = OfflinePlatesPerHour * SpeedFactor;
                int space = WarehouseCap - _data.warehousePlates;
                granted = Mathf.Min(Mathf.FloorToInt((float)(hours * rate)), space);
                if (granted > 0)
                {
                    _data.warehousePlates += granted;
                    EchoStore.Save();
                }
            }
            TouchLastSeen();
            if (granted > 0)
            {
                Toast.Show("离线产出：星律板 ×" + granted + "（离线 " + hours.ToString("F1") +
                    " 小时，已存入仓库，记得收取）");
                Debug.Log("[Workshop] 离线结算：+" + granted + " 板（离线 " + hours.ToString("F1") + "h）");
            }
            return granted;
        }

        private void TouchLastSeen()
        {
            if (_data == null) return;
            _data.lastSeenTicks = System.DateTime.UtcNow.Ticks;
            EchoStore.Save();
        }

        // ---------------- 收取 ----------------

        public int CollectPlates()
        {
            int n = _data.warehousePlates;
            if (n <= 0) return 0;
            _data.warehousePlates = 0;
            _data.starPlates += n;
            EchoStore.Save();
            Toast.Show("收取星律板 ×" + n + "（可用于回响强化）");
            Debug.Log("[Workshop] 收取星律板 ×" + n + "，星律板现 ×" + _data.starPlates);
            return n;
        }

        // ---------------- 生产（空间物流） ----------------

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.B)) ToggleBuildMode();
            if (BuildMode)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) ToggleBuildMode();
                else HandleBuildInput();
            }

            if (Running)
            {
                ProductionStep(Time.deltaTime);
            }

            _saveT += Time.deltaTime;
            if (_saveT >= 60f)
            {
                _saveT = 0f;
                TouchLastSeen();
            }
        }

        // 出货方向 = 设施本地 +X（rotY=0 → +X，90 → -Z，180 → -X，270 → +Z）。
        // 标准线沿 +X 布设，默认 rotY=0 即可顺流。
        private static Vector3 FlowDir(Facility f)
        {
            float r = f.rotY * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(r), 0f, -Mathf.Sin(r));
        }

        // 找出货方向上最近的设施（前方、横向偏差 < 4m、距离 < 25m）。
        private Facility FindNext(Facility from)
        {
            if (Facilities == null) return null;
            Vector3 d = FlowDir(from);
            Facility best = null;
            float bestFwd = float.MaxValue;
            foreach (var q in Facilities)
            {
                if (q == from) continue;
                Vector3 to = q.pos - from.pos;
                float fwd = Vector3.Dot(to, d);
                if (fwd <= 0.5f || fwd > 25f) continue;
                float lat = (to - d * fwd).magnitude;
                if (lat > 4f) continue;
                if (fwd < bestFwd) { bestFwd = fwd; best = q; }
            }
            return best;
        }

        private bool CanAccept(Facility target, string kind)
        {
            if (target == null) return false;
            switch (target.type)
            {
                case TypeBelt:
                    return target.items != null && target.items.Count < BeltItemCap;
                case TypeProcessor:
                    // 加工站只收矿石（进输入缓冲）。
                    return kind == "ore" && target.ore < ProcInputCap;
                case TypeWarehouse:
                    // 仓库只收星律板。
                    return kind == "plate" && _data != null && _data.warehousePlates < WarehouseCap;
                default:
                    return false;
            }
        }

        private void Accept(Facility target, string kind)
        {
            switch (target.type)
            {
                case TypeBelt:
                    target.items.Add(new BeltItem { kind = kind, t = 0f });
                    break;
                case TypeProcessor:
                    target.ore++;
                    break;
                case TypeWarehouse:
                    _data.warehousePlates++;
                    EchoStore.Save();
                    break;
            }
        }

        // 尝试把一件物品推给出货方向上的下一个设施。成功返回 true。
        private bool TryPush(Facility from, string kind)
        {
            var next = FindNext(from);
            if (next == null || !CanAccept(next, kind)) return false;
            Accept(next, kind);
            return true;
        }

        private void ProductionStep(float dt)
        {
            float sdt = dt * SpeedFactor;
            if (Facilities == null) return;
            // 先处理输送带（物品移动），再处理生产与推出，避免同一帧
            // 内新入带物品被重复推进。
            foreach (var f in Facilities) if (f.type == TypeBelt) UpdateBelt(f, sdt);
            foreach (var f in Facilities)
            {
                if (f.type == TypeMiner) UpdateMiner(f, sdt);
                else if (f.type == TypeProcessor) UpdateProcessor(f, sdt);
            }
            foreach (var f in Facilities) if (f.type == TypeBelt) SyncBeltItems(f);
        }

        private void UpdateMiner(Facility f, float sdt)
        {
            // 生产：1 矿/3s，输出缓冲上限 3（满了就停产，背压）。
            if (f.ore < MinerOutCap)
            {
                f.timer += sdt;
                while (f.timer >= 3f && f.ore < MinerOutCap)
                {
                    f.timer -= 3f;
                    f.ore++;
                }
                if (f.timer >= 3f) f.timer = 0f;
            }
            // 推出：有矿就往出货方向推，推不出去就留在缓冲里。
            if (f.ore > 0 && TryPush(f, "ore")) f.ore--;
        }

        private void UpdateProcessor(Facility f, float sdt)
        {
            // 加工：输入有矿且输出未满时，1 矿 → 1 板/6s。
            if (f.ore > 0 && f.plates < ProcOutCap)
            {
                f.timer += sdt;
                while (f.timer >= 6f && f.ore > 0 && f.plates < ProcOutCap)
                {
                    f.timer -= 6f;
                    f.ore--;
                    f.plates++;
                }
                if (f.timer >= 6f) f.timer = 0f;
            }
            // 推出：有板就往出货方向推（仓库或输送带）。
            if (f.plates > 0 && TryPush(f, "plate")) f.plates--;
        }

        private void UpdateBelt(Facility f, float sdt)
        {
            if (f.items == null || f.items.Count == 0) return;
            const float spacing = 0.34f;
            // 从最前（t 最大）往后处理，后件不超越前件。
            for (int i = 0; i < f.items.Count; i++)
            {
                var it = f.items[i];
                float maxT = i == 0 ? 1f : f.items[i - 1].t - spacing;
                if (maxT < it.t) maxT = it.t; // 已被前件顶住时不后退
                it.t = Mathf.Min(it.t + sdt / BeltTransitTime, maxT);
            }
            // 到达末端的前件尝试推出；推不出去就停在 t=1 等待。
            while (f.items.Count > 0 && f.items[0].t >= 1f)
            {
                if (TryPush(f, f.items[0].kind)) f.items.RemoveAt(0);
                else break;
            }
        }

        private void SyncBeltItems(Facility f)
        {
            if (f == null || f.items == null || f.itemMeshes == null) return;
            // 补齐 / 清理可视化小方块，与 items 列表一一对应。
            while (f.itemMeshes.Count < f.items.Count)
            {
                var m = GameObject.CreatePrimitive(PrimitiveType.Cube);
                m.transform.SetParent(f.root.transform, false);
                m.transform.localScale = Vector3.one * 0.55f;
                Object.Destroy(m.GetComponent<BoxCollider>());
                f.itemMeshes.Add(m);
            }
            while (f.itemMeshes.Count > f.items.Count)
            {
                var m = f.itemMeshes[f.itemMeshes.Count - 1];
                f.itemMeshes.RemoveAt(f.itemMeshes.Count - 1);
                if (m != null) Destroy(m);
            }
            for (int i = 0; i < f.items.Count; i++)
            {
                var it = f.items[i];
                var mesh = f.itemMeshes[i];
                if (mesh == null) continue;
                mesh.SetActive(true);
                var rend = mesh.GetComponent<Renderer>();
                if (rend != null)
                    rend.material = it.kind == "plate" ? PlateMaterial() : OreMaterial();
                // 沿带本地 X 轴：-6（入带）→ +6（出带），高 1.0m。
                mesh.transform.localPosition = new Vector3(-6f + 12f * it.t, 1.0f, 0f);
            }
        }

        private static Material _oreMat;
        private static Material _plateMat;

        private static Material OreMaterial()
        {
            if (_oreMat == null)
                _oreMat = TideSong.MaterialLibrary.Make(new Color(0.35f, 0.85f, 1f), 0.1f, 0.6f);
            return _oreMat;
        }

        private static Material PlateMaterial()
        {
            if (_plateMat == null)
                _plateMat = TideSong.MaterialLibrary.Make(new Color(0.85f, 0.80f, 0.67f), 0.3f, 0.5f);
            return _plateMat;
        }

        // ---------------- 建造模式 ----------------

        public void ToggleBuildMode()
        {
            BuildMode = !BuildMode;
            GhostType = -1;
            SetSelected(null);
            if (_ghost != null) { Destroy(_ghost); _ghost = null; }
            if (BuildMode)
            {
                // 俯视建造视角（终末地式，约 76° 下视）
                if (_player != null)
                    _player.SetExternalView(
                        new Vector3(PlotCenter.x, PlotCenter.y + 160f, PlotCenter.z + 40f),
                        PlotCenter);
                Toast.Show("建造模式：数字键 1–5 选设施，左键放置/选中，R 旋转，X 拆除，B 退出");
                Debug.Log("[Workshop] 进入建造模式");
            }
            else
            {
                if (_player != null) _player.ClearExternalView();
                Debug.Log("[Workshop] 退出建造模式");
            }
        }

        private void HandleBuildInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectGhostType(TypeMiner);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) SelectGhostType(TypeBelt);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) SelectGhostType(TypeProcessor);
            else if (Input.GetKeyDown(KeyCode.Alpha4)) SelectGhostType(TypeWarehouse);
            else if (Input.GetKeyDown(KeyCode.Alpha5)) SelectGhostType(TypePylon);

            if (Input.GetKeyDown(KeyCode.R)) RotateSelected();
            if (Input.GetKeyDown(KeyCode.X) && Selected != null)
            {
                var f = Selected;
                RemoveFacility(f);
                Toast.Show("已拆除 " + TypeName(f.type));
            }

            UpdateGhost();
            if (Input.GetMouseButtonDown(0))
            {
                if (GhostType >= 0 && GhostValid)
                {
                    AddFacility(GhostType, GhostPos, 0f);
                    Toast.Show("已放置 " + TypeName(GhostType) + "（功耗 " + PowerUsed + "/" + PowerCapacity + "）");
                }
                else if (GhostType < 0)
                {
                    SelectAtMouse();
                }
            }
        }

        private void SelectGhostType(int type)
        {
            GhostType = type;
            if (_ghost != null) Destroy(_ghost);
            _ghost = new GameObject("WorkshopGhost");
            BuildVisual(_ghost.transform, type);
            foreach (var r in _ghost.GetComponentsInChildren<Renderer>())
            {
                r.material = TideSong.MaterialLibrary.MakeTransparent(
                    new Color(0.55f, 0.9f, 1f, 0.45f), 0f, 0.5f);
            }
            foreach (var c in _ghost.GetComponentsInChildren<Collider>()) Destroy(c);
            SetSelected(null);
        }

        private void UpdateGhost()
        {
            if (GhostType < 0 || _ghost == null) { GhostValid = false; return; }
            Vector3 hit = MouseGroundPoint();
            if (hit == Vector3.zero) { GhostValid = false; _ghost.SetActive(false); return; }
            float x = Mathf.Clamp(Mathf.Round(hit.x / 5f) * 5f,
                PlotCenter.x - PlotHalfX + 2f, PlotCenter.x + PlotHalfX - 2f);
            float z = Mathf.Clamp(Mathf.Round(hit.z / 5f) * 5f,
                PlotCenter.z - PlotHalfZ + 2f, PlotCenter.z + PlotHalfZ - 2f);
            float ground = WorldMapTerrain.SampleHeight(x, z);
            GhostPos = new Vector3(x, ground, z);
            GhostValid = !OverlapsFacility(GhostPos);
            _ghost.SetActive(true);
            _ghost.transform.position = GhostPos;
            foreach (var r in _ghost.GetComponentsInChildren<Renderer>())
            {
                r.material.color = GhostValid
                    ? new Color(0.55f, 0.9f, 1f, 0.45f)
                    : new Color(1f, 0.35f, 0.35f, 0.45f);
            }
        }

        private bool OverlapsFacility(Vector3 pos)
        {
            if (Facilities == null) return false;
            foreach (var f in Facilities)
            {
                Vector2 a = new Vector2(f.pos.x, f.pos.z);
                Vector2 b = new Vector2(pos.x, pos.z);
                if (Vector2.Distance(a, b) < 6f) return true;
            }
            return false;
        }

        private void SelectAtMouse()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Facility best = null;
            float bestD = 8f;
            foreach (var f in Facilities)
            {
                Vector3 sp = cam.WorldToScreenPoint(f.pos + Vector3.up * 1.5f);
                if (sp.z < 0f) continue;
                float d = Vector2.Distance(new Vector2(sp.x, sp.y),
                    new Vector2(Input.mousePosition.x, Input.mousePosition.y));
                if (d < bestD) { bestD = d; best = f; }
            }
            SetSelected(best);
        }

        private void SetSelected(Facility f)
        {
            if (Selected != null && Selected.highlight != null) Selected.highlight.SetActive(false);
            Selected = f;
            if (Selected != null && Selected.highlight != null) Selected.highlight.SetActive(true);
        }

        private Vector3 MouseGroundPoint()
        {
            var cam = Camera.main;
            if (cam == null) return Vector3.zero;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            // 先射地形，射不到则用 1100 m 水平面兜底
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 2000f)) return hit.point;
            float planeY = PlotCenter.y;
            if (Mathf.Abs(ray.direction.y) < 0.001f) return Vector3.zero;
            float t = (planeY - ray.origin.y) / ray.direction.y;
            if (t < 0f) return Vector3.zero;
            return ray.origin + ray.direction * t;
        }

        // ---------------- 测试钩子（smoke 用） ----------------

        public void TestPlaceStandardLine()
        {
            while (Facilities.Count > 0) RemoveFacility(Facilities[0]);
            // 仓库是跨运行持久化的（EchoStore）：不清空会导致快进生产因满仓
            // 而停产（2026-11-18 复测：仓库 50/50，快进 60s 产 0 板）。
            // 测试从空仓开始，保证产量断言有意义。
            _data.warehousePlates = 0;
            EchoStore.Save();
            PlaceStandardLine();
        }

        // 造一个超载态：标准线功耗 65，再加两台加工站（+80）→ 145 > 100。
        // v1 不再停机，生产以降速 45% 继续（TestFastForward 可量到约 45% 产量）。
        public void TestForceOverload()
        {
            AddFacility(TypeProcessor, new Vector3(2942f, 0f, -303f), 0f);
            AddFacility(TypeProcessor, new Vector3(2962f, 0f, -303f), 0f);
        }

        public int TestSetOfflineHours(float hours)
        {
            _data.lastSeenTicks = System.DateTime.UtcNow.AddHours(-hours).Ticks;
            EchoStore.Save();
            return SettleOffline();
        }

        public int TestFastForward(float seconds)
        {
            if (!Running) return 0; // 生产线不完整时不生产
            // 超载时以 45% 速度生产（v1 不再停机）。
            int before = _data.warehousePlates;
            float t = 0f;
            while (t < seconds)
            {
                ProductionStep(1f);
                t += 1f;
            }
            return _data.warehousePlates - before;
        }
    }
}
