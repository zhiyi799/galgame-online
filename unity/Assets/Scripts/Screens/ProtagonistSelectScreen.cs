// ProtagonistSelectScreen — dual-gender protagonist selection (ASTRAL BLOOM).
// Wuthering Waves Rover-style: the player picks the male OR the female
// 无星者 at game start. Both are the SAME story character (same identity,
// ability, past mystery, plot) — gender only changes the presentation
// (art / animation set). No face-customisation.
//
// Art: loaded by resource name from the protagonist config
// ("Characters/<art_resource>"). The user generates the art themselves
// ("人物等我自己生"), so until the real art arrives each slot shows an
// explicit placeholder (立绘待补充）. Dropping protagonist_male.png /
// protagonist_female.png into Assets/Resources/Characters/ makes the real
// art appear with no code change. Never use another character's art here.
//
// Flow: Auth -> (no selection) ProtagonistSelect -> MainMenu; (selection
// saved) Auth -> MainMenu directly. The choice is saved by
// ProtagonistManager (PlayerPrefs + local save file) and survives restarts.
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GalgameOnline.Core;
using GalgameOnline.UI;

namespace GalgameOnline.Screens
{
    public class ProtagonistSelectScreen : MonoBehaviour
    {
        // Layout (1280x720 reference): two 340x420 panels side by side.
        private const float PanelWidth = 340f;
        private const float PanelHeight = 420f;
        private const float PanelTopY = -150f;
        private const float ArtHeight = 292f;

        private GameObject _malePanel;
        private GameObject _femalePanel;
        private CanvasGroup _maleGroup;
        private CanvasGroup _femaleGroup;
        private TextMeshProUGUI _maleTag;
        private TextMeshProUGUI _femaleTag;
        private Button _maleButton;
        private Button _femaleButton;
        private Button _confirmButton;
        private TextMeshProUGUI _confirmText;

        private string _previewGender; // "male" / "female" / null
        private Coroutine _transition;

        private void Start()
        {
            var canvas = UiFactory.SceneCanvas();
            UiFactory.AddBackground(canvas.transform, "Default", UiFactory.DeepUltramarine);
            var root = UiFactory.Panel(canvas.transform, "SelectRoot",
                Vector2.zero, Vector2.one, Vector2.zero, UiFactory.Glass(0.82f));

            // Soft moonlight glow (restrained: one low-alpha radial glow).
            var glow = UiFactory.ArtImage(root.transform, "MoonGlow", UiFactory.RadialGlowSprite());
            if (glow != null)
            {
                var gRt = glow.GetComponent<RectTransform>();
                gRt.anchorMin = new Vector2(0.5f, 1); gRt.anchorMax = new Vector2(0.5f, 1);
                gRt.pivot = new Vector2(0.5f, 0.5f);
                gRt.anchoredPosition = new Vector2(0, -120);
                gRt.sizeDelta = new Vector2(700, 700);
                glow.color = new Color(0.47f, 0.59f, 0.86f, 0.08f);
            }

            var title = UiFactory.Label(root.transform, "Title", "ASTRAL BLOOM", 44,
                TextAlignmentOptions.Center, UiFactory.MoonWhite, true);
            title.rectTransform.anchorMin = new Vector2(0.5f, 1);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1);
            UiFactory.Place(title.rectTransform, -300, -30, 600, 56);

            var sub = UiFactory.Label(root.transform, "Subtitle", "星樱物语", 20,
                TextAlignmentOptions.Center, UiFactory.SilverWhite);
            sub.rectTransform.anchorMin = new Vector2(0.5f, 1);
            sub.rectTransform.anchorMax = new Vector2(0.5f, 1);
            UiFactory.Place(sub.rectTransform, -300, -82, 600, 28);

            var prompt = UiFactory.Label(root.transform, "Prompt", "请选择与你一同踏上星辰旅途的伙伴。", 18,
                TextAlignmentOptions.Center, UiFactory.MoonWhite);
            prompt.rectTransform.anchorMin = new Vector2(0.5f, 1);
            prompt.rectTransform.anchorMax = new Vector2(0.5f, 1);
            UiFactory.Place(prompt.rectTransform, -400, -114, 800, 26);

            _malePanel = BuildGenderPanel(root.transform, ProtagonistManager.Male, "男主角", -250f);
            _femalePanel = BuildGenderPanel(root.transform, ProtagonistManager.Female, "女主角", 250f);
            _maleGroup = _malePanel.GetComponent<CanvasGroup>();
            _femaleGroup = _femalePanel.GetComponent<CanvasGroup>();
            _maleTag = _malePanel.transform.Find("SelectedTag").GetComponent<TextMeshProUGUI>();
            _femaleTag = _femalePanel.transform.Find("SelectedTag").GetComponent<TextMeshProUGUI>();
            _maleButton = _malePanel.GetComponent<Button>();
            _femaleButton = _femalePanel.GetComponent<Button>();

            _confirmButton = UiFactory.Button(root.transform, "ConfirmButton", "确认选择",
                OnConfirm, new Vector2(420, 52));
            var cRt = _confirmButton.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0.5f, 1); cRt.anchorMax = new Vector2(0.5f, 1);
            cRt.anchoredPosition = new Vector2(0, -600);
            _confirmText = _confirmButton.GetComponentInChildren<TextMeshProUGUI>();

            var hint = UiFactory.Label(root.transform, "Hint",
                "首次选择后将保存，再次进入游戏时将直接加载已选择的主角。", 13,
                TextAlignmentOptions.Center, UiFactory.SilverWhite);
            hint.rectTransform.anchorMin = new Vector2(0.5f, 1);
            hint.rectTransform.anchorMax = new Vector2(0.5f, 1);
            UiFactory.Place(hint.rectTransform, -400, -664, 800, 20);

            var core = UiFactory.Label(root.transform, "CoreLine",
                "「无论以怎样的模样踏上旅途，你都是那个被星辰记住的人。」", 13,
                TextAlignmentOptions.Center, UiFactory.PaleGold);
            core.rectTransform.anchorMin = new Vector2(0.5f, 1);
            core.rectTransform.anchorMax = new Vector2(0.5f, 1);
            UiFactory.Place(core.rectTransform, -500, -688, 1000, 20);

            // Pre-select the saved gender (if any) so a returning player
            // sees their choice; first-time players start unselected.
            if (ProtagonistManager.HasSelection)
                PreviewGender(ProtagonistManager.SelectedGender, animate: false);
            else
                UpdateSelectionVisuals();

            // Fade the whole screen in.
            var rootGroup = root.AddComponent<CanvasGroup>();
            StartCoroutine(FadeIn(rootGroup));

            if (GameManager.SmokeMode)
                SmokeTestRunner.Run(this);
        }

        // One gender panel: art area (real art or explicit placeholder) +
        // name / trait / intro. Returns the panel GameObject (a Button).
        private GameObject BuildGenderPanel(Transform root, ProtagonistManager.ProtagonistEntry entry,
            string genderLabel, float centerX)
        {
            var panel = UiFactory.Panel(root.transform, "Panel_" + entry.gender,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(PanelWidth, PanelHeight), UiFactory.Glass(0.55f));
            var pRt = panel.GetComponent<RectTransform>();
            pRt.pivot = new Vector2(0.5f, 1);
            pRt.anchoredPosition = new Vector2(centerX, PanelTopY);
            UiFactory.AddSilverBorder(pRt);
            panel.AddComponent<CanvasGroup>();

            // The panel itself is the click target.
            var btn = panel.AddComponent<Button>();
            btn.transition = Button.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.94f, 1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.84f, 0.95f, 1f);
            colors.disabledColor = Color.white;
            btn.colors = colors;
            string gender = entry.gender;
            btn.onClick.AddListener(() => PreviewGender(gender, animate: true));

            // Art area: real art by resource name, or an explicit placeholder.
            var artArea = UiFactory.Panel(panel.transform, "ArtArea",
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, ArtHeight),
                UiFactory.GlassWhite(0.06f));
            var aRt = artArea.GetComponent<RectTransform>();
            aRt.pivot = new Vector2(0.5f, 1);
            aRt.offsetMin = new Vector2(10, -ArtHeight - 10);
            aRt.offsetMax = new Vector2(-10, -10);

            Sprite art = null;
            if (!string.IsNullOrEmpty(entry.art_resource))
            {
                var tex = Resources.Load<Texture2D>("Characters/" + entry.art_resource);
                if (tex != null)
                    art = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            if (art != null)
            {
                var img = UiFactory.ArtImage(artArea.transform, "Art", art, true);
                UiFactory.Stretch(img.GetComponent<RectTransform>());
            }
            else
            {
                // Explicit placeholder — the user supplies the art later.
                var star = UiFactory.Label(artArea.transform, "PlaceholderStar", "★", 40,
                    TextAlignmentOptions.Center, UiFactory.MoonlightBlue);
                star.rectTransform.anchorMin = new Vector2(0.5f, 1);
                star.rectTransform.anchorMax = new Vector2(0.5f, 1);
                UiFactory.Place(star.rectTransform, -40, -100, 80, 48);
                var ph = UiFactory.Label(artArea.transform, "PlaceholderText", "立绘待补充", 20,
                    TextAlignmentOptions.Center, UiFactory.SilverWhite);
                ph.rectTransform.anchorMin = new Vector2(0.5f, 1);
                ph.rectTransform.anchorMax = new Vector2(0.5f, 1);
                UiFactory.Place(ph.rectTransform, -100, -152, 200, 28);
                var phSub = UiFactory.Label(artArea.transform, "PlaceholderSub", genderLabel, 15,
                    TextAlignmentOptions.Center, UiFactory.MoonlightBlue);
                phSub.rectTransform.anchorMin = new Vector2(0.5f, 1);
                phSub.rectTransform.anchorMax = new Vector2(0.5f, 1);
                UiFactory.Place(phSub.rectTransform, -100, -184, 200, 22);
            }

            // "已选择" tag (top-right of the panel, hidden until selected).
            var tag = UiFactory.Label(panel.transform, "SelectedTag", "已选择", 14,
                TextAlignmentOptions.Center, UiFactory.PaleGold, true);
            tag.rectTransform.anchorMin = new Vector2(1, 1);
            tag.rectTransform.anchorMax = new Vector2(1, 1);
            tag.rectTransform.pivot = new Vector2(1, 1);
            UiFactory.Place(tag.rectTransform, -14, -10, 70, 20);
            tag.gameObject.SetActive(false);

            // Name / trait / intro (below the art area).
            var name = UiFactory.Label(panel.transform, "Name", entry.name + " · " + genderLabel, 24,
                TextAlignmentOptions.Center, UiFactory.MoonWhite, true);
            name.rectTransform.anchorMin = new Vector2(0.5f, 1);
            name.rectTransform.anchorMax = new Vector2(0.5f, 1);
            UiFactory.Place(name.rectTransform, -160, -312, 320, 32);

            var trait = UiFactory.Label(panel.transform, "Trait", entry.trait, 14,
                TextAlignmentOptions.Center, UiFactory.MoonlightBlue);
            trait.rectTransform.anchorMin = new Vector2(0.5f, 1);
            trait.rectTransform.anchorMax = new Vector2(0.5f, 1);
            UiFactory.Place(trait.rectTransform, -160, -344, 320, 20);

            var intro = UiFactory.Label(panel.transform, "Intro", entry.intro, 15,
                TextAlignmentOptions.Center, UiFactory.SilverWhite);
            intro.rectTransform.anchorMin = new Vector2(0.5f, 1);
            intro.rectTransform.anchorMax = new Vector2(0.5f, 1);
            intro.rectTransform.sizeDelta = new Vector2(300, 48);
            intro.rectTransform.anchoredPosition = new Vector2(0, -368);
            return panel;
        }

        /// <summary>Preview a gender (highlight + transition animation).
        /// Does NOT save — saving happens on 确认选择.</summary>
        public void PreviewGender(string gender, bool animate = true)
        {
            if (gender != ProtagonistManager.GenderMale && gender != ProtagonistManager.GenderFemale)
                return;
            bool changed = _previewGender != gender;
            _previewGender = gender;
            UpdateSelectionVisuals();
            if (animate && changed)
            {
                if (_transition != null) StopCoroutine(_transition);
                _transition = StartCoroutine(SwitchTransition(gender));
            }
        }

        /// <summary>Smoke-test hook: select + save without navigating.</summary>
        public bool ConfirmForTest(string gender)
        {
            PreviewGender(gender, animate: false);
            return ProtagonistManager.SelectGender(gender);
        }

        private void UpdateSelectionVisuals()
        {
            bool maleSel = _previewGender == ProtagonistManager.GenderMale;
            bool femaleSel = _previewGender == ProtagonistManager.GenderFemale;
            SetPanelSelected(_malePanel, _maleTag, maleSel);
            SetPanelSelected(_femalePanel, _femaleTag, femaleSel);
            if (_confirmButton != null)
            {
                _confirmButton.interactable = _previewGender != null;
                if (_confirmText != null)
                    _confirmText.color = _previewGender != null
                        ? UiFactory.MoonWhite
                        : new Color(0.898f, 0.918f, 0.969f, 0.45f);
            }
        }

        private static void SetPanelSelected(GameObject panel, TextMeshProUGUI tag, bool selected)
        {
            if (panel == null) return;
            var img = panel.GetComponent<Image>();
            if (img != null)
                img.color = selected
                    ? new Color(0.141f, 0.302f, 0.671f, 0.75f) // ClassicUltramarine
                    : UiFactory.Glass(0.55f);
            if (tag != null) tag.gameObject.SetActive(selected);
        }

        private void OnConfirm()
        {
            if (string.IsNullOrEmpty(_previewGender))
            {
                Toast.Show("请先选择一位主角。");
                return;
            }
            if (!ProtagonistManager.SelectGender(_previewGender))
            {
                Toast.Show("保存失败，请重试。");
                return;
            }
            Toast.Show("已选择" + (_previewGender == ProtagonistManager.GenderFemale ? "女主角" : "男主角")
                + " · 无星者。");
            GameManager.Instance.GoTo(GameManager.SceneMainMenu);
        }

        // Transition animation on switch: the newly selected panel fades in
        // with a small scale pulse; the other panel dims briefly.
        private IEnumerator SwitchTransition(string gender)
        {
            var newly = gender == ProtagonistManager.GenderMale ? _maleGroup : _femaleGroup;
            var other = gender == ProtagonistManager.GenderMale ? _femaleGroup : _maleGroup;
            var newlyRt = (gender == ProtagonistManager.GenderMale ? _malePanel : _femalePanel)
                .GetComponent<RectTransform>();
            float t = 0f;
            const float dur = 0.32f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                if (newly != null) newly.alpha = 0.55f + 0.45f * k;
                if (other != null) other.alpha = 1f - 0.15f * Mathf.Sin(k * Mathf.PI);
                if (newlyRt != null)
                {
                    float s = 1f + 0.02f * Mathf.Sin(k * Mathf.PI);
                    newlyRt.localScale = new Vector3(s, s, 1f);
                }
                yield return null;
            }
            if (newly != null) newly.alpha = 1f;
            if (other != null) other.alpha = 1f;
            if (newlyRt != null) newlyRt.localScale = Vector3.one;
            _transition = null;
        }

        private static IEnumerator FadeIn(CanvasGroup group)
        {
            if (group == null) yield break;
            group.alpha = 0f;
            float t = 0f;
            const float dur = 0.4f;
            while (t < dur)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.Clamp01(t / dur);
                yield return null;
            }
            group.alpha = 1f;
        }
    }
}
