using UnityEngine;

namespace TideSong
{
    /// <summary>
    /// 在通用骨架上按角色定义做二次改造：配色、发色、武器形态、属性光环。
    /// 基础骨架仍然来自 CharacterBuilder，保证和原版工程一致。
    /// </summary>
    public static class ResonatorVisuals
    {
        public static CharacterRig Build(Transform parent, ResonatorDef def)
        {
            var rig = CharacterBuilder.Build(parent, def.primary);

            // 主体配色
            Paint(rig.body, def.primary, false);
            Paint(rig.legL, Color.Lerp(def.primary, Color.black, 0.35f), false);
            Paint(rig.legR, Color.Lerp(def.primary, Color.black, 0.35f), false);

            // 发色
            Paint(rig.hairBack, def.accent, false);
            Paint(rig.hairL, def.accent, false);
            Paint(rig.hairR, def.accent, false);

            // 属性光环
            BuildAura(rig, def);

            // 武器
            RebuildWeapon(rig, def);

            // 体型微差：岚更壮、霜见更瘦
            float bulk = def.id == "lan" ? 1.06f : (def.id == "shuang" ? 0.95f : 1f);
            rig.visual.localScale = new Vector3(bulk, 1f, bulk);

            return rig;
        }

        static void Paint(Transform t, Color c, bool glow)
        {
            if (t == null) return;
            var r = t.GetComponent<Renderer>();
            if (r == null) return;
            var m = r.material;
            m.color = c;
            if (glow) MaterialLibrary.ApplyGlow(m, c * 1.6f);
        }

        static void BuildAura(CharacterRig rig, ResonatorDef def)
        {
            // 背后的共鸣环
            var ringMat = MaterialLibrary.MakeTransparent(new Color(def.elementColor.r, def.elementColor.g, def.elementColor.b, 0.45f), 0.1f, 0.6f);
            var ring = MaterialLibrary.Prim(PrimitiveType.Cylinder, rig.visual, ringMat,
                new Vector3(0f, 1.12f, -0.30f), new Vector3(0.86f, 0.008f, 0.86f),
                Quaternion.Euler(90f, 0f, 0f));
            ring.name = "ResonanceRing";
            ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.AddComponent<RingSpin>().speed = 55f;

            // 腰带上的属性结晶
            var gemMat = MaterialLibrary.Make(def.elementColor, 0.2f, 0.8f);
            MaterialLibrary.ApplyGlow(gemMat, def.elementColor * 2.2f);
            var gem = MaterialLibrary.Prim(PrimitiveType.Cube, rig.visual, gemMat,
                new Vector3(0f, 0.88f, 0.30f), new Vector3(0.10f, 0.10f, 0.06f),
                Quaternion.Euler(45f, 0f, 45f));
            gem.name = "ElementGem";

            // 脚下的微弱光环，方便在草地里看清自己
            var footMat = MaterialLibrary.MakeTransparent(new Color(def.elementColor.r, def.elementColor.g, def.elementColor.b, 0.28f), 0f, 0.5f);
            var foot = MaterialLibrary.Prim(PrimitiveType.Cylinder, rig.visual, footMat,
                new Vector3(0f, 0.02f, 0f), new Vector3(0.95f, 0.004f, 0.95f));
            foot.name = "FootGlow";
            foot.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static void RebuildWeapon(CharacterRig rig, ResonatorDef def)
        {
            if (rig.weapon != null) Object.Destroy(rig.weapon.gameObject);

            var holder = new GameObject("Weapon").transform;
            holder.SetParent(rig.hiltGrip, false);
            holder.localRotation = Quaternion.Euler(70f, 0f, 0f);
            rig.weapon = holder;

            var dark = MaterialLibrary.Make(new Color(0.16f, 0.16f, 0.20f), 0.4f, 0.5f);
            var metal = MaterialLibrary.Make(new Color(0.80f, 0.84f, 0.90f), 0.9f, 0.85f);
            var glow = MaterialLibrary.Make(def.elementColor, 0.1f, 0.7f);
            MaterialLibrary.ApplyGlow(glow, def.elementColor * 2.4f);

            switch (def.weapon)
            {
                case WeaponStyle.Sword:
                    MaterialLibrary.Prim(PrimitiveType.Cylinder, holder, dark, Vector3.zero, new Vector3(0.07f, 0.16f, 0.07f));
                    MaterialLibrary.Prim(PrimitiveType.Cube, holder, MaterialLibrary.Gold, new Vector3(0f, 0.20f, 0f), new Vector3(0.26f, 0.05f, 0.10f));
                    MaterialLibrary.Prim(PrimitiveType.Cube, holder, metal, new Vector3(0f, 0.74f, 0f), new Vector3(0.10f, 1.14f, 0.045f));
                    MaterialLibrary.Prim(PrimitiveType.Cube, holder, glow, new Vector3(0f, 0.74f, 0f), new Vector3(0.115f, 1.10f, 0.02f));
                    break;

                case WeaponStyle.Guns:
                    for (int i = 0; i < 2; i++)
                    {
                        float sx = i == 0 ? 1f : -1f;
                        var gun = new GameObject("Gun" + i).transform;
                        gun.SetParent(holder, false);
                        gun.localPosition = new Vector3(sx * 0.02f, 0.05f, 0f);
                        gun.localRotation = Quaternion.Euler(0f, 0f, sx * -8f);
                        MaterialLibrary.Prim(PrimitiveType.Cube, gun, dark, new Vector3(0f, 0.16f, 0f), new Vector3(0.09f, 0.34f, 0.12f));
                        MaterialLibrary.Prim(PrimitiveType.Cylinder, gun, metal, new Vector3(0f, 0.40f, 0f), new Vector3(0.045f, 0.20f, 0.045f));
                        MaterialLibrary.Prim(PrimitiveType.Cube, gun, glow, new Vector3(0f, 0.58f, 0f), new Vector3(0.05f, 0.035f, 0.05f));
                    }
                    break;

                case WeaponStyle.Gauntlet:
                    MaterialLibrary.Prim(PrimitiveType.Cube, holder, dark, new Vector3(0f, 0.22f, 0f), new Vector3(0.30f, 0.34f, 0.30f));
                    MaterialLibrary.Prim(PrimitiveType.Cube, holder, metal, new Vector3(0f, 0.30f, 0.05f), new Vector3(0.34f, 0.16f, 0.34f));
                    MaterialLibrary.Prim(PrimitiveType.Cube, holder, glow, new Vector3(0f, 0.40f, 0.16f), new Vector3(0.16f, 0.05f, 0.05f));
                    break;

                case WeaponStyle.Scythe:
                    MaterialLibrary.Prim(PrimitiveType.Cylinder, holder, dark, new Vector3(0f, 0.55f, 0f), new Vector3(0.055f, 1.15f, 0.055f));
                    MaterialLibrary.Prim(PrimitiveType.Cube, holder, metal, new Vector3(0.26f, 1.62f, 0f), new Vector3(0.52f, 0.11f, 0.045f), Quaternion.Euler(0f, 0f, -18f));
                    MaterialLibrary.Prim(PrimitiveType.Cube, holder, glow, new Vector3(0.52f, 1.50f, 0f), new Vector3(0.40f, 0.07f, 0.03f), Quaternion.Euler(0f, 0f, -36f));
                    MaterialLibrary.Prim(PrimitiveType.Cube, holder, metal, new Vector3(-0.12f, 1.72f, 0f), new Vector3(0.20f, 0.09f, 0.05f), Quaternion.Euler(0f, 0f, 16f));
                    break;
            }
        }
    }
}
