using UnityEngine;

namespace TideSong
{
    /// <summary>用基础几何体拼一个二次元风格剑士，带可动关节。</summary>
    public class CharacterRig
    {
        public GameObject root;      // 逻辑根（贴地位置）
        public Transform visual;     // 视觉根（用于倾斜、受击抖动）
        public Transform body, head, hairBack, hairL, hairR;
        public Transform armL, armR, legL, legR;
        public Transform weapon;
        public Transform weaponRest; // 剑挂在背上时的父节点
        public Transform hiltGrip;   // 手持点
        public float height = 1.78f;
    }

    public static class CharacterBuilder
    {
        public static CharacterRig Build(Transform parent, Color clothColor)
        {
            var rig = new CharacterRig();
            rig.root = new GameObject("PlayerVisual");
            rig.root.transform.SetParent(parent, false);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(rig.root.transform, false);
            rig.visual = visual;

            var bodyMat = MaterialLibrary.MakeToon(clothColor);
            var trimMat = MaterialLibrary.MakeToon(Color.Lerp(clothColor, Color.white, 0.55f));
            var skin = MaterialLibrary.Skin;
            var hair = MaterialLibrary.Hair;
            var boots = MaterialLibrary.MakeToon(new Color(0.18f, 0.20f, 0.28f), outline: 0.016f);

            // 躯干
            var torso = MaterialLibrary.Prim(PrimitiveType.Capsule, visual, bodyMat, new Vector3(0f, 1.06f, 0f), new Vector3(0.58f, 0.42f, 0.40f));
            rig.body = torso.transform;

            // 短裙 / 下摆
            MaterialLibrary.Prim(PrimitiveType.Cylinder, visual, trimMat, new Vector3(0f, 0.72f, 0f), new Vector3(0.52f, 0.16f, 0.48f));
            // 腰带
            MaterialLibrary.Prim(PrimitiveType.Cylinder, visual, MaterialLibrary.Gold, new Vector3(0f, 0.90f, 0f), new Vector3(0.52f, 0.045f, 0.47f));

            // 头
            var head = MaterialLibrary.Prim(PrimitiveType.Sphere, visual, skin, new Vector3(0f, 1.63f, 0f), new Vector3(0.40f, 0.44f, 0.40f));
            rig.head = head.transform;

            // 头发：后脑 + 两侧双马尾
            rig.hairBack = MaterialLibrary.Prim(PrimitiveType.Sphere, head.transform, hair,
                new Vector3(0f, 0.10f, -0.06f), new Vector3(1.06f, 1.02f, 1.10f)).transform;
            rig.hairL = MaterialLibrary.Prim(PrimitiveType.Capsule, head.transform, hair,
                new Vector3(-0.30f, -0.32f, -0.04f), new Vector3(0.16f, 0.34f, 0.16f),
                Quaternion.Euler(0f, 0f, 14f)).transform;
            rig.hairR = MaterialLibrary.Prim(PrimitiveType.Capsule, head.transform, hair,
                new Vector3(0.30f, -0.32f, -0.04f), new Vector3(0.16f, 0.34f, 0.16f),
                Quaternion.Euler(0f, 0f, -14f)).transform;
            // 呆毛
            MaterialLibrary.Prim(PrimitiveType.Capsule, head.transform, hair,
                new Vector3(0.02f, 0.30f, 0.02f), new Vector3(0.05f, 0.14f, 0.05f), Quaternion.Euler(0f, 0f, 18f));

            // 眼睛
            var eyeMat = MaterialLibrary.Make(Color.white, 0f, 0.9f);
            foreach (float sx in new[] { -0.13f, 0.13f })
            {
                MaterialLibrary.Prim(PrimitiveType.Sphere, head.transform, eyeMat,
                    new Vector3(sx, 0.01f, 0.40f), new Vector3(0.13f, 0.17f, 0.06f));
                var pupil = MaterialLibrary.Prim(PrimitiveType.Sphere, head.transform, MaterialLibrary.Anchor,
                    new Vector3(sx, 0.01f, 0.435f), new Vector3(0.075f, 0.10f, 0.05f));
            }

            // 手臂（挂在躯干上，方便摆动）
            rig.armL = MaterialLibrary.Prim(PrimitiveType.Capsule, visual, skin,
                new Vector3(-0.34f, 1.18f, 0f), new Vector3(0.14f, 0.26f, 0.14f)).transform;
            rig.armR = MaterialLibrary.Prim(PrimitiveType.Capsule, visual, skin,
                new Vector3(0.34f, 1.18f, 0f), new Vector3(0.14f, 0.26f, 0.14f)).transform;
            // 肩甲
            MaterialLibrary.Prim(PrimitiveType.Sphere, visual, trimMat, new Vector3(-0.34f, 1.42f, 0f), new Vector3(0.20f, 0.14f, 0.20f));
            MaterialLibrary.Prim(PrimitiveType.Sphere, visual, trimMat, new Vector3(0.34f, 1.42f, 0f), new Vector3(0.20f, 0.14f, 0.20f));

            // 腿
            rig.legL = MaterialLibrary.Prim(PrimitiveType.Capsule, visual, MaterialLibrary.MakeToon(new Color(0.20f, 0.26f, 0.40f), outline: 0.016f),
                new Vector3(-0.15f, 0.46f, 0f), new Vector3(0.17f, 0.30f, 0.17f)).transform;
            rig.legR = MaterialLibrary.Prim(PrimitiveType.Capsule, visual, MaterialLibrary.MakeToon(new Color(0.20f, 0.26f, 0.40f), outline: 0.016f),
                new Vector3(0.15f, 0.46f, 0f), new Vector3(0.17f, 0.30f, 0.17f)).transform;
            // 靴
            MaterialLibrary.Prim(PrimitiveType.Cube, visual, boots, new Vector3(-0.15f, 0.16f, 0.05f), new Vector3(0.24f, 0.22f, 0.34f));
            MaterialLibrary.Prim(PrimitiveType.Cube, visual, boots, new Vector3(0.15f, 0.16f, 0.05f), new Vector3(0.24f, 0.22f, 0.34f));

            // 剑：握在右手
            var grip = new GameObject("Grip").transform;
            grip.SetParent(rig.armR, false);
            grip.localPosition = new Vector3(0f, -0.24f, 0.06f);
            rig.hiltGrip = grip;

            var sword = new GameObject("Sword").transform;
            sword.SetParent(grip, false);
            sword.localRotation = Quaternion.Euler(70f, 0f, 0f);
            rig.weapon = sword;

            MaterialLibrary.Prim(PrimitiveType.Cylinder, sword, MaterialLibrary.Make(new Color(0.2f, 0.15f, 0.12f)),
                new Vector3(0f, 0f, 0f), new Vector3(0.07f, 0.16f, 0.07f));
            MaterialLibrary.Prim(PrimitiveType.Cube, sword, MaterialLibrary.Gold,
                new Vector3(0f, 0.20f, 0f), new Vector3(0.26f, 0.05f, 0.10f));
            var blade = MaterialLibrary.Prim(PrimitiveType.Cube, sword, MaterialLibrary.Metal,
                new Vector3(0f, 0.72f, 0f), new Vector3(0.10f, 1.10f, 0.045f));
            var glowBlade = MaterialLibrary.Prim(PrimitiveType.Cube, sword, MaterialLibrary.Glow,
                new Vector3(0f, 0.72f, 0f), new Vector3(0.115f, 1.06f, 0.02f));
            glowBlade.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            return rig;
        }

        /// <summary>怪物：漂浮的暗星之影。</summary>
        public static Transform BuildEnemy(Transform parent, float scale = 1f)
        {
            var root = new GameObject("EnemyVisual");
            root.transform.SetParent(parent, false);

            var core = MaterialLibrary.Prim(PrimitiveType.Sphere, root.transform, MaterialLibrary.Enemy,
                Vector3.zero, new Vector3(0.85f, 0.85f, 0.85f));
            var shell = MaterialLibrary.Prim(PrimitiveType.Sphere, root.transform, MaterialLibrary.EnemyDark,
                Vector3.zero, new Vector3(1.05f, 1.05f, 1.05f));
            shell.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 发光眼
            MaterialLibrary.Prim(PrimitiveType.Sphere, root.transform, MaterialLibrary.Make(new Color(1f, 0.25f, 0.45f)),
                new Vector3(0f, 0.06f, 0.42f), new Vector3(0.22f, 0.13f, 0.10f));

            // 尖刺
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                var s = MaterialLibrary.Prim(PrimitiveType.Cube, root.transform, MaterialLibrary.Enemy,
                    new Vector3(Mathf.Cos(a) * 0.55f, Mathf.Sin(a) * 0.2f, Mathf.Sin(a) * 0.55f),
                    new Vector3(0.16f, 0.16f, 0.55f));
                s.transform.LookAt(root.transform.position);
            }

            // 环绕光环
            var ring = MaterialLibrary.Prim(PrimitiveType.Cylinder, root.transform, MaterialLibrary.MakeTransparent(new Color(0.25f, 0.41f, 0.79f, 0.5f), 0f, 0.5f),
                new Vector3(0f, -0.5f, 0f), new Vector3(1.5f, 0.01f, 1.5f));
            ring.name = "Ring";

            root.transform.localScale = Vector3.one * scale;
            return root.transform;
        }
    }
}
