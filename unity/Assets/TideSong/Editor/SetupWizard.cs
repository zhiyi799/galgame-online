using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TideSong.EditorTools
{
    /// <summary>菜单栏工具：一键生成可运行的主场景 / 清空存档。</summary>
    public static class SetupWizard
    {
        [MenuItem("TideSong/Rebuild Main Scene")]
        public static void RebuildMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var boot = new GameObject("Bootstrap");
            boot.AddComponent<GameBootstrap>();

            Directory.CreateDirectory("Assets/Scenes");
            bool ok = EditorSceneManager.SaveScene(scene, "Assets/Scenes/Exploration.unity");
            AssetDatabase.Refresh();

            if (ok)
            {
                EditorUtility.DisplayDialog("TideSong",
                    "主场景已生成：Assets/Scenes/Exploration.unity\n点 Play 就能玩。", "好");
            }
            else
            {
                EditorUtility.DisplayDialog("TideSong", "场景保存失败，请检查 Assets/Scenes 目录权限。", "好");
            }
        }

        [MenuItem("TideSong/Delete Save")]
        public static void DeleteSave()
        {
            SaveSystem.Clear();
            EditorUtility.DisplayDialog("TideSong", "存档已清空。", "好");
        }

        [MenuItem("TideSong/Set Player Start Speed x2 (Debug)")]
        public static void DebugSpeed()
        {
            var p = Object.FindObjectOfType<PlayerCharacter>();
            if (p == null) { EditorUtility.DisplayDialog("TideSong", "先点 Play 进入运行状态。", "好"); return; }
            p.walkSpeed *= 2f;
            p.sprintSpeed *= 1.5f;
            Debug.Log("[TideSong] 移动速度已提升");
        }
    }
}
