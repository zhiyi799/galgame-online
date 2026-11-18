// TerrainEdgeCheck — one-off diagnostic for the yongye/skyvault seam (P1-D).
// Reads the built TerrainData assets and prints the edge column heights
// around z=-569 (row 256) plus the same check for a known-good seam.
using UnityEditor;
using UnityEngine;

namespace GalgameOnline.Editor
{
    public static class TerrainEdgeCheck
    {
        [MenuItem("GalgameOnline/Check Terrain Edges (P1-D)")]
        public static void CheckEdges()
        {
            TerrainData yongye = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/WorldMap/TerrainData/yongye.asset");
            TerrainData skyvault = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/WorldMap/TerrainData/skyvault.asset");
            TerrainData xinghuan = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/WorldMap/TerrainData/xinghuan.asset");
            TerrainData wuhai = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/WorldMap/TerrainData/wuhai.asset");
            Debug.Log($"[EdgeCheck] resolutions: yongye={yongye.heightmapResolution} skyvault={skyvault.heightmapResolution} xinghuan={xinghuan.heightmapResolution} wuhai={wuhai.heightmapResolution}");
            Debug.Log($"[EdgeCheck] sizes: yongye={yongye.size} skyvault={skyvault.size}");

            // Row for z=-569 on yongye/skyvault (originZ=-1138, depth 1138): row = 569/1138*512 = 256.
            float[,] yh = yongye.GetHeights(0, 0, 513, 513);
            float[,] sh = skyvault.GetHeights(0, 0, 513, 513);
            Debug.Log("[EdgeCheck] yongye E edge (u=512) vs skyvault W edge (u=0), rows 250..262 (z ~ -580..-558):");
            for (int row = 250; row <= 262; row++)
            {
                float ye = yh[row, 512] * 600f;
                float sw = sh[row, 0] * 1400f;
                Debug.Log($"[EdgeCheck] row {row}: yongyeE={ye:F1} skyvaultW={sw:F1} diff={Mathf.Abs(ye - sw):F1}");
            }
            // Heights 5m/1m either side of the edge at z=-569 (row 256).
            // Column for x=1995 on yongye (originX=0): u = 1995/2000*512 = 510.7 -> 511.
            // Column for x=2001 on skyvault (originX=2000): u = 1/2000*512 = 0.26 -> 0.
            Debug.Log("[EdgeCheck] near-edge heights at z=-569 (row 256):");
            Debug.Log($"[EdgeCheck] yongye x=1995 (u=511): {yh[256, 511] * 600f:F1}  x=1999 (u=512): {yh[256, 512] * 600f:F1}");
            Debug.Log($"[EdgeCheck] skyvault x=2001 (u=0): {sh[256, 0] * 1400f:F1}  x=2005 (u=1): {sh[256, 1] * 1400f:F1}");

            // Control: known-good seam xinghuan N (v=512) vs wuhai S (v=0), col for x=500 (u=128).
            float[,] xh = xinghuan.GetHeights(0, 0, 513, 513);
            float[,] wh = wuhai.GetHeights(0, 0, 513, 513);
            Debug.Log($"[EdgeCheck] control xinghuan N edge (v=512, u=128, x=500): {xh[512, 128] * 200f:F1}  wuhai S edge (v=0, u=128): {wh[0, 384] * 600f:F1}");
        }
    }
}
