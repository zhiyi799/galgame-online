using UnityEngine;

namespace TideSong
{
    /// <summary>可交互物件基类（宝箱 / 锚点 / 采集物 / 祭坛）。</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public abstract string Prompt { get; }
        public virtual bool CanInteract { get { return true; } }
        public abstract void Interact(PlayerCharacter player);

        protected virtual void Start()
        {
            if (GameManager.Instance != null) GameManager.Instance.Register(this);
        }
    }

    /// <summary>宝箱：开箱给结晶与经验，带一次开盖动画。</summary>
    public class TreasureChest : Interactable
    {
        public Transform lid;
        public bool opened;
        public int crystals = 3;
        public int exp = 12;

        public override string Prompt { get { return opened ? "空箱子" : "打开宝箱"; } }
        public override bool CanInteract { get { return !opened; } }

        public override void Interact(PlayerCharacter p)
        {
            if (opened) return;
            opened = true;
            GameHUD.Instance?.Toast("获得 星律结晶 x" + crystals + " · 经验 +" + exp);
            p.CrystalCount += crystals;
            p.ChestOpened++;
            p.AddExp(exp);
            p.AddEnergy(12f);
            Sfx.Play(SfxId.Chest, transform.position);
            FX.Burst(transform.position + Vector3.up * 0.8f, new Color(1f, 0.85f, 0.4f), 34, 5f, 0.32f, 0.9f);
            FX.Shockwave(transform.position, new Color(1f, 0.9f, 0.5f), 3.5f, 0.4f);
            if (lid != null) StartCoroutine(OpenLid());
        }

        System.Collections.IEnumerator OpenLid()
        {
            var from = lid.localRotation;
            var to = Quaternion.Euler(-105f, 0f, 0f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 3.2f;
                lid.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
        }
    }

    /// <summary>星律锚点：激活后成为传送 / 休整点。</summary>
    public class TideShrine : Interactable
    {
        public bool activated;
        public Transform crystal;
        public Light glow;
        public int index;

        public override string Prompt { get { return activated ? "在锚点休整 · 回满状态" : "点亮星律锚点"; } }

        public override void Interact(PlayerCharacter p)
        {
            if (!activated)
            {
                activated = true;
                p.ShrinesLit++;
                p.CrystalCount += 2;
                p.AddExp(30);
                Sfx.Play(SfxId.Shrine, transform.position);
                FX.Burst(transform.position + Vector3.up * 1.5f, new Color(0.45f, 0.95f, 1f), 60, 8f, 0.45f, 1.2f);
                FX.Shockwave(transform.position, new Color(0.5f, 0.95f, 1f), 7f, 0.7f);
                if (glow != null) glow.intensity = 4.5f;
                GameHUD.Instance?.Toast("星律锚点已点亮 " + p.ShrinesLit + " / 3");
                GameManager.Instance.SpawnPoint = transform.position + transform.forward * 2f + Vector3.up * 1.2f;
            }
            else
            {
                p.hp = p.maxHP;
                p.stamina = p.maxStamina;
                Sfx.Play(SfxId.Shrine, transform.position);
                FX.Burst(transform.position + Vector3.up * 1.2f, new Color(0.6f, 1f, 0.9f), 26, 4f, 0.3f, 0.8f);
                GameHUD.Instance?.Toast("状态已恢复");
            }
        }

        void Update()
        {
            if (crystal != null)
            {
                crystal.Rotate(Vector3.up, 24f * Time.deltaTime, Space.World);
                crystal.localPosition = new Vector3(0f, 2.1f + Mathf.Sin(Time.time * 1.6f) * 0.16f, 0f);
            }
            if (glow != null && activated)
                glow.intensity = 3.4f + Mathf.Sin(Time.time * 2.4f) * 0.5f;
        }
    }

    /// <summary>采集点：摘了过一会儿再长回来。</summary>
    public class GatherNode : Interactable
    {
        public GameObject visual;
        public bool available = true;
        float respawnT;

        public override string Prompt { get { return available ? "采集 · 星生草" : "刚刚采过"; } }
        public override bool CanInteract { get { return available; } }

        public override void Interact(PlayerCharacter p)
        {
            if (!available) return;
            available = false;
            respawnT = 16f;
            if (visual != null) visual.SetActive(false);
            p.AddEnergy(8f);
            p.AddStamina(12f);
            p.AddExp(3);
            p.CrystalCount += 1;
            Sfx.Play(SfxId.Pickup, transform.position);
            FX.Burst(transform.position + Vector3.up * 0.4f, new Color(0.6f, 1f, 0.7f), 14, 3f, 0.22f, 0.6f);
            GameHUD.Instance?.Toast("采集 星生草 x1");
        }

        void Update()
        {
            if (!available)
            {
                respawnT -= Time.deltaTime;
                if (respawnT <= 0f)
                {
                    available = true;
                    if (visual != null) visual.SetActive(true);
                }
            }
            else if (visual != null)
            {
                visual.transform.Rotate(Vector3.up, 22f * Time.deltaTime, Space.World);
            }
        }
    }

    /// <summary>中央祭坛：主线目标，点亮 3 座锚点 + 攒够结晶后共鸣。</summary>
    public class TideAltar : Interactable
    {
        public bool completed;
        public Transform core;
        public Light glow;

        public override string Prompt
        {
            get
            {
                if (completed) return "星樱回响 · 已完成";
                var p = PlayerCharacter.Instance;
                if (p == null) return "星律祭坛";
                if (p.ShrinesLit < 3) return "祭坛沉寂（锚点 " + p.ShrinesLit + "/3）";
                if (p.CrystalCount < 15) return "需要星律结晶 " + p.CrystalCount + "/15";
                return "唤醒星律祭坛";
            }
        }

        public override bool CanInteract
        {
            get
            {
                if (completed) return false;
                var p = PlayerCharacter.Instance;
                return p != null && p.ShrinesLit >= 3 && p.CrystalCount >= 15;
            }
        }

        public override void Interact(PlayerCharacter p)
        {
            if (completed) return;
            completed = true;
            p.CrystalCount -= 15;
            p.AddExp(150);
            Sfx.Play(SfxId.Burst, transform.position);
            FX.Shockwave(transform.position, new Color(0.6f, 1f, 1f), 16f, 1.2f);
            FX.Burst(transform.position + Vector3.up * 3f, new Color(0.7f, 1f, 1f), 120, 12f, 0.6f, 2f);
            if (glow != null) glow.intensity = 8f;
            GameHUD.Instance?.Toast("星樱回响 · 完成");
            GameHUD.Instance?.ShowVictory();
            GameManager.Instance.SpawnWave(transform.position, 6);
        }

        void Update()
        {
            if (core != null)
            {
                core.Rotate(Vector3.up, 30f * Time.deltaTime, Space.World);
                core.localPosition = new Vector3(0f, 2.6f + Mathf.Sin(Time.time * 1.2f) * 0.25f, 0f);
            }
            if (glow != null)
                glow.intensity = (completed ? 6f : 2.2f) + Mathf.Sin(Time.time * 2f) * 0.4f;
        }
    }
}
