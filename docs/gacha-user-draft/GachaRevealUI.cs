using System;
using UnityEngine;
using UnityEngine.UI;

public class GachaRevealUI : MonoBehaviour
{
    [Header("References")]
    public GameObject revealCanvas;
    public GachaAnimationController animationController;
    public Button skipButton;

    private Action currentFinished;

    private void Awake()
    {
        if (revealCanvas != null)
            revealCanvas.SetActive(false);

        if (skipButton != null)
        {
            skipButton.onClick.RemoveListener(Skip);
            skipButton.onClick.AddListener(Skip);
        }

        if (animationController == null)
        {
            animationController =
                GetComponentInChildren<GachaAnimationController>(true);
        }
    }

    public void Reveal(
        GachaPullResult result,
        Action onFinished)
    {
        if (result == null)
        {
            onFinished?.Invoke();
            return;
        }

        if (animationController == null)
        {
            Debug.LogError("没有找到 GachaAnimationController。");
            onFinished?.Invoke();
            return;
        }

        currentFinished = onFinished;

        if (revealCanvas != null)
            revealCanvas.SetActive(true);

        animationController.Play(result, FinishCurrent);
    }

    private void FinishCurrent()
    {
        Action callback = currentFinished;
        currentFinished = null;

        callback?.Invoke();
    }

    public void Skip()
    {
        if (animationController != null)
        {
            animationController.Skip();
            return;
        }

        FinishCurrent();
    }

    public void Hide()
    {
        currentFinished = null;

        if (revealCanvas != null)
            revealCanvas.SetActive(false);
    }

    private void OnDestroy()
    {
        if (skipButton != null)
            skipButton.onClick.RemoveListener(Skip);
    }
}
