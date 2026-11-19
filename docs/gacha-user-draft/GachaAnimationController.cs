using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AstralBloom.Gacha
{
    public class GachaAnimationController : MonoBehaviour
    {
        [Header("演出 UI")]
        public CanvasGroup overlay;
        public Image background;
        public Image flash;
        public Image rarityRing;
        public Image characterPortrait;

        public Text rarityText;
        public Text characterName;

        [Header("颜色")]
        public Color threeStarColor =
            new Color(0.35f, 0.65f, 0.95f);

        public Color fourStarColor =
            new Color(0.65f, 0.4f, 1f);

        public Color fiveStarColor =
            new Color(1f, 0.76f, 0.25f);

        [Header("演出时间")]
        public float normalDuration = 1.2f;
        public float fiveStarDuration = 2.8f;

        private Coroutine currentAnimation;
        private bool skipRequested;

        public void Play(
            GachaPullResult result,
            Action onFinished)
        {
            if (currentAnimation != null)
                StopCoroutine(currentAnimation);

            skipRequested = false;

            currentAnimation = StartCoroutine(
                PlayRoutine(result, onFinished)
            );
        }

        public void Skip()
        {
            skipRequested = true;
        }

        private IEnumerator PlayRoutine(
            GachaPullResult result,
            Action onFinished)
        {
            if (overlay == null)
            {
                onFinished?.Invoke();
                yield break;
            }

            overlay.gameObject.SetActive(true);
            overlay.alpha = 1;

            Color rarityColor = GetRarityColor(
                result.character.rarity
            );

            if (background != null)
                background.color = rarityColor * 0.3f;

            if (rarityRing != null)
                rarityRing.color = rarityColor;

            if (characterPortrait != null)
            {
                characterPortrait.sprite =
                    result.character.portrait;

                characterPortrait.color =
                    new Color(1, 1, 1, 0);

                characterPortrait.gameObject.SetActive(true);
            }

            if (rarityText != null)
            {
                rarityText.text =
                    GetRarityLabel(
                        result.character.rarity
                    );

                rarityText.color = rarityColor;
            }

            if (characterName != null)
            {
                characterName.text =
                    result.character.displayName;

                characterName.color = Color.white;
            }

            float duration =
                result.character.rarity ==
                Rarity.FiveStar
                ? fiveStarDuration
                : normalDuration;

            float timer = 0;

            while (timer < duration && !skipRequested)
            {
                timer += Time.deltaTime;

                float progress = Mathf.Clamp01(
                    timer / duration
                );

                // 角色立绘渐显
                if (characterPortrait != null)
                {
                    Color c = characterPortrait.color;
                    c.a = Mathf.SmoothStep(
                        0,
                        1,
                        progress
                    );
                    characterPortrait.color = c;

                    float scale = Mathf.Lerp(
                        1.12f,
                        1f,
                        progress
                    );

                    characterPortrait.rectTransform.localScale =
                        Vector3.one * scale;
                }

                // 闪光
                if (flash != null)
                {
                    float flashAlpha =
                        Mathf.Max(
                            0,
                            1 - progress * 4
                        );

                    flash.color = new Color(
                        1,
                        1,
                        1,
                        flashAlpha
                    );
                }

                // 光环旋转
                if (rarityRing != null)
                {
                    rarityRing.rectTransform.Rotate(
                        Vector3.forward,
                        -60 * Time.deltaTime
                    );

                    float scale = Mathf.Lerp(
                        0.5f,
                        1.2f,
                        progress
                    );

                    rarityRing.rectTransform.localScale =
                        Vector3.one * scale;
                }

                yield return null;
            }

            if (characterPortrait != null)
            {
                Color c = characterPortrait.color;
                c.a = 1;
                characterPortrait.color = c;
            }

            if (flash != null)
                flash.color = new Color(1, 1, 1, 0);

            currentAnimation = null;

            onFinished?.Invoke();
        }

        private Color GetRarityColor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.FiveStar:
                    return fiveStarColor;

                case Rarity.FourStar:
                    return fourStarColor;

                default:
                    return threeStarColor;
            }
        }

        private string GetRarityLabel(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.FiveStar:
                    return "★★★★★  LEGENDARY";

                case Rarity.FourStar:
                    return "★★★★  EPIC";

                default:
                    return "★★★  RARE";
            }
        }
    }
}
