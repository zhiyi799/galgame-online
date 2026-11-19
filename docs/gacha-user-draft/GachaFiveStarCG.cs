using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GachaFiveStarCG : MonoBehaviour
{
    [Header("CG 面板")]
    public CanvasGroup canvasGroup;

    [Header("画面")]
    public Image background;
    public Image characterArtwork;
    public Image lightOverlay;

    [Header("文字")]
    public Text characterName;
    public Text rarityLabel;

    [Header("交互")]
    public Button continueButton;
    public Button skipButton;

    [Header("演出参数")]
    public float fadeDuration = 0.7f;
    public float artworkScaleStart = 0.85f;
    public float artworkScaleEnd = 1f;

    private Coroutine animationCoroutine;
    private Action onFinished;
    private bool playing;

    private void Awake()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            canvasGroup.gameObject.SetActive(false);
        }

        if (continueButton != null)
            continueButton.onClick.AddListener(Finish);

        if (skipButton != null)
            skipButton.onClick.AddListener(Finish);
    }

    public bool Play(GachaPullResult result, Action finished)
    {
        if (result == null ||
            result.character == null ||
            result.character.rarity != Rarity.FiveStar)
        {
            return false;
        }

        if (playing)
            return false;

        onFinished = finished;
        playing = true;

        if (canvasGroup != null)
        {
            canvasGroup.gameObject.SetActive(true);
            canvasGroup.alpha = 0;
        }

        if (characterName != null)
            characterName.text = result.character.characterName;

        if (rarityLabel != null)
            rarityLabel.text = "★★★★★  FIVE STAR";

        if (characterArtwork != null &&
            result.character.portrait != null)
        {
            characterArtwork.sprite = result.character.portrait;
            characterArtwork.color = Color.white;

            characterArtwork.rectTransform.localScale =
                Vector3.one * artworkScaleStart;
        }

        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);

        animationCoroutine = StartCoroutine(PlayRoutine());

        return true;
    }

    private IEnumerator PlayRoutine()
    {
        float timer = 0;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / fadeDuration);
            float smooth = Mathf.SmoothStep(0, 1, t);

            if (canvasGroup != null)
                canvasGroup.alpha = smooth;

            if (characterArtwork != null)
            {
                float scale = Mathf.Lerp(
                    artworkScaleStart,
                    artworkScaleEnd,
                    smooth
                );

                characterArtwork.rectTransform.localScale =
                    Vector3.one * scale;
            }

            if (lightOverlay != null)
            {
                Color color = lightOverlay.color;
                color.a = smooth * 0.65f;
                lightOverlay.color = color;
            }

            yield return null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 1;

        animationCoroutine = null;
    }

    public void Finish()
    {
        if (!playing)
            return;

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            canvasGroup.gameObject.SetActive(false);
        }

        playing = false;

        Action callback = onFinished;
        onFinished = null;

        callback?.Invoke();
    }

    private void OnDestroy()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(Finish);

        if (skipButton != null)
            skipButton.onClick.RemoveListener(Finish);
    }
}
