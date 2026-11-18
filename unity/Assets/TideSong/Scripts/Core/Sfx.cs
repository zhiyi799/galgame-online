using System.Collections.Generic;
using UnityEngine;

namespace TideSong
{
    public enum SfxId
    {
        Swing, Heavy, Hurt, Dodge, Jump, Glide, Skill, Burst, Pickup, Chest, Hit, UI, Step, Shrine
    }

    /// <summary>所有音效在运行时用波形合成，不依赖任何音频文件。</summary>
    public static class Sfx
    {
        const int SampleRate = 44100;

        static AudioSource[] pool;
        static int cursor;
        static Dictionary<SfxId, AudioClip> clips = new Dictionary<SfxId, AudioClip>();
        static bool ready;

        public static float Volume = 0.55f;

        public static void Init(Transform parent)
        {
            if (ready) return;
            ready = true;

            var root = new GameObject("SfxPool").transform;
            root.SetParent(parent, false);
            pool = new AudioSource[10];
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("s" + i);
                go.transform.SetParent(root, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0.35f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.maxDistance = 40f;
                src.volume = 1f;
                pool[i] = src;
            }

            clips[SfxId.Swing] = Whoosh("swing", 0.20f, 900f, 2600f, 0.55f);
            clips[SfxId.Heavy] = Impact("heavy", 0.34f, 150f, 46f, 0.7f, 0.35f);
            clips[SfxId.Hit] = Impact("hit", 0.16f, 520f, 180f, 0.6f, 0.25f);
            clips[SfxId.Hurt] = Impact("hurt", 0.28f, 320f, 90f, 0.45f, 0.3f);
            clips[SfxId.Dodge] = Whoosh("dodge", 0.26f, 400f, 1800f, 0.35f);
            clips[SfxId.Jump] = Tone("jump", 0.16f, 420f, 760f, 0.30f);
            clips[SfxId.Glide] = Whoosh("glide", 0.55f, 300f, 900f, 0.25f);
            clips[SfxId.Skill] = Sweep("skill", 0.55f, 180f, 980f, 0.55f, 0.35f);
            clips[SfxId.Burst] = Sweep("burst", 1.10f, 90f, 1400f, 0.65f, 0.45f);
            clips[SfxId.Pickup] = Arp("pickup", new[] { 880f, 1320f }, 0.07f);
            clips[SfxId.Chest] = Arp("chest", new[] { 523f, 659f, 784f, 1046f }, 0.09f);
            clips[SfxId.UI] = Tone("ui", 0.06f, 1200f, 1200f, 0.18f);
            clips[SfxId.Step] = Noise("step", 0.09f, 0.22f);
            clips[SfxId.Shrine] = Arp("shrine", new[] { 392f, 523f, 659f, 880f, 1174f }, 0.13f);
        }

        public static void Play(SfxId id, Vector3 pos)
        {
            if (!ready || pool == null) return;
            AudioClip clip;
            if (!clips.TryGetValue(id, out clip) || clip == null) return;
            var src = pool[cursor];
            cursor = (cursor + 1) % pool.Length;
            src.transform.position = pos;
            src.pitch = Random.Range(0.94f, 1.07f);
            src.PlayOneShot(clip, Volume);
        }

        public static void Play2D(SfxId id)
        {
            Play(id, Vector3.zero);
        }

        // ---------------- 波形合成 ----------------

        static AudioClip Build(string name, float dur, System.Func<float, float, float> gen)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(dur * SampleRate));
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                data[i] = Mathf.Clamp(gen(t, i / (float)count), -1f, 1f);
            }
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Tone(string name, float dur, float f0, float f1, float vol)
        {
            return Build(name, dur, (t, k) =>
            {
                float f = Mathf.Lerp(f0, f1, k);
                float env = Mathf.Exp(-6f * k) * Mathf.Min(1f, k * 40f);
                return Mathf.Sin(2f * Mathf.PI * f * t) * env * vol;
            });
        }

        static AudioClip Sweep(string name, float dur, float f0, float f1, float vol, float noise)
        {
            float lp = 0f;
            return Build(name, dur, (t, k) =>
            {
                float f = Mathf.Lerp(f0, f1, Mathf.Pow(k, 0.6f));
                float env = Mathf.Exp(-3.2f * k) * Mathf.Min(1f, k * 22f);
                float s = Mathf.Sin(2f * Mathf.PI * f * t) * 0.7f;
                s += Mathf.Sin(2f * Mathf.PI * f * 1.5f * t) * 0.25f;
                float n = Random.value * 2f - 1f;
                lp += (n - lp) * 0.25f;
                return (s + lp * noise) * env * vol;
            });
        }

        static AudioClip Impact(string name, float dur, float f0, float f1, float vol, float noise)
        {
            float lp = 0f;
            return Build(name, dur, (t, k) =>
            {
                float f = Mathf.Lerp(f0, f1, Mathf.Sqrt(k));
                float env = Mathf.Exp(-9f * k);
                float s = Mathf.Sin(2f * Mathf.PI * f * t);
                float n = Random.value * 2f - 1f;
                lp += (n - lp) * 0.4f;
                return (s * 0.85f + lp * noise) * env * vol;
            });
        }

        static AudioClip Whoosh(string name, float dur, float f0, float f1, float vol)
        {
            float bp = 0f, lp = 0f;
            return Build(name, dur, (t, k) =>
            {
                float n = Random.value * 2f - 1f;
                lp += (n - lp) * 0.12f;
                bp = lp - (bp * 0.9f);
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(k)) * vol;
                env *= Mathf.Clamp01(k * 6f);
                float f = Mathf.Lerp(f0, f1, k);
                return (bp * 0.8f + Mathf.Sin(2f * Mathf.PI * f * 0.02f * t) * 0.1f) * env;
            });
        }

        static AudioClip Noise(string name, float dur, float vol)
        {
            return Build(name, dur, (t, k) =>
            {
                float env = Mathf.Exp(-14f * k);
                return (Random.value * 2f - 1f) * env * vol;
            });
        }

        static AudioClip Arp(string name, float[] notes, float step)
        {
            float dur = notes.Length * step;
            return Build(name, dur, (t, k) =>
            {
                int idx = Mathf.Clamp(Mathf.FloorToInt(t / step), 0, notes.Length - 1);
                float local = (t - idx * step) / step;
                float env = Mathf.Exp(-5f * local) * Mathf.Min(1f, local * 30f);
                float f = notes[idx];
                return Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.35f;
            });
        }
    }
}
