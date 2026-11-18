// WorldMapTerrain — static registry + height sampling for the WorldMap scene.
// Five Unity Terrains built from the 513x513 RAW heightmaps (see
// docs/world-map-final/高度图生成记录.md). Tiles are placed edge-to-edge
// (no gaps) so the continent is walkable; seams are blended in the Editor
// builder (WorldMapBuilder.cs). Do NOT use TideSong's WorldData here.
using UnityEngine;

namespace GalgameOnline.WorldMap
{
    public static class WorldMapTerrain
    {
        public struct RegionDef
        {
            public string key;        // terrain key, matches RAW file + TerrainData asset
            public string name;       // display name
            public string rawFile;    // RAW file under tools/heightmap/out/
            public float maxHeight;   // terrain height in metres (= region max elevation)
            public float originX;     // world x of the tile's west edge (south-west corner)
            public float originZ;     // world z of the tile's south edge
            public float waterLevel;  // water plane height in metres
            public Color waterColor;  // water tint (alpha included)
        }

        // Single tile: 2000 m (x) x 1138 m (z), aspect 1.757 from the heightmap
        // crop — NOT stretched to a 2000x2000 square.
        public const float TileWidth = 2000f;
        public const float TileDepth = 1138f;

        // Contiguous half-offset layout (see Unity地形重建报告.md for why the
        // suggested diagonal coordinates were adjusted): every neighbouring
        // pair shares a full edge, so seams can be blended exactly.
        //   xinghuan (0,0) centre; wuhai NW, baita NE, skyvault SE, yongye S.
        public static readonly RegionDef[] Regions = new[]
        {
            new RegionDef { key = "xinghuan",  name = "星环城",   rawFile = "xinghuan_513.raw",  maxHeight = 200f,  originX = 0f,     originZ = 0f,     waterLevel = 2f,   waterColor = new Color(0.10f, 0.22f, 0.45f, 0.62f) },
            new RegionDef { key = "wuhai",     name = "雾海边境", rawFile = "wuhai_513.raw",     maxHeight = 600f,  originX = -1000f, originZ = 1138f,  waterLevel = 2f,   waterColor = new Color(0.62f, 0.74f, 0.82f, 0.72f) },
            new RegionDef { key = "baita",     name = "白塔遗域", rawFile = "baita_513.raw",     maxHeight = 420f,  originX = 1000f,  originZ = 1138f,  waterLevel = 2f,   waterColor = new Color(0.62f, 0.74f, 0.82f, 0.72f) },
            new RegionDef { key = "skyvault",  name = "苍穹工业区", rawFile = "skyvault_513.raw", maxHeight = 1400f, originX = 2000f,  originZ = -1138f, waterLevel = 315f,  waterColor = new Color(0.10f, 0.22f, 0.45f, 0.62f) },
            new RegionDef { key = "yongye",    name = "永夜之森", rawFile = "yongye_513.raw",    maxHeight = 600f,  originX = 0f,     originZ = -1138f, waterLevel = 2f,   waterColor = new Color(0.08f, 0.20f, 0.32f, 0.62f) },
        };

        private static Terrain[] _terrains;

        public static void Register()
        {
            _terrains = Object.FindObjectsOfType<Terrain>();
        }

        public static int TerrainCount => _terrains != null ? _terrains.Length : 0;

        /// <summary>Ground height at (x, z) across all five tiles.
        /// Returns false when the point is outside every tile.</summary>
        public static bool SampleHeight(float x, float z, out float h)
        {
            h = 0f;
            if (_terrains == null) Register();
            if (_terrains == null) return false;
            foreach (var t in _terrains)
            {
                if (t == null || t.terrainData == null) continue;
                Vector3 p = t.transform.position;
                Vector3 size = t.terrainData.size;
                if (x >= p.x && x <= p.x + size.x && z >= p.z && z <= p.z + size.z)
                {
                    h = t.SampleHeight(new Vector3(x, 0f, z));
                    return true;
                }
            }
            return false;
        }

        public static float SampleHeight(float x, float z)
        {
            float h;
            return SampleHeight(x, z, out h) ? h : 0f;
        }

        public static bool IsOnTerrain(float x, float z)
        {
            float h;
            return SampleHeight(x, z, out h);
        }

        public static string RegionAt(float x, float z)
        {
            foreach (var r in Regions)
                if (x >= r.originX && x < r.originX + TileWidth
                    && z >= r.originZ && z < r.originZ + TileDepth)
                    return r.name;
            return "大陆之外";
        }

        // Spawn: Xinghuan central plaza (tile centre). Height is sampled at
        // runtime so the player always stands on the terrain.
        public static Vector3 SpawnPoint
        {
            get
            {
                float h = SampleHeight(1000f, 569f);
                return new Vector3(1000f, h + 1.2f, 569f);
            }
        }
    }
}
