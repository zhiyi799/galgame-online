using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GachaSequencePlayer : MonoBehaviour
{
    [Header("References")]
    public GachaRevealUI revealUI;

    private readonly Queue<GachaPullResult> queue =
        new Queue<GachaPullResult>();

    private Action onSequenceCompleted;
    private Coroutine sequenceCoroutine;
    private bool isPlaying;

    public bool IsPlaying => isPlaying;

    public void PlaySequence(
        List<GachaPullResult> results,
        Action completed = null)
    {
        if (results == null || results.Count == 0)
        {
            completed?.Invoke();
            return;
        }

        if (isPlaying)
        {
            Debug.LogWarning("当前抽卡演出尚未结束。");
            return;
        }

        queue.Clear();

        foreach (GachaPullResult result in results)
        {
            if (result != null)
                queue.Enqueue(result);
        }

        onSequenceCompleted = completed;
        sequenceCoroutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        isPlaying = true;

        while (queue.Count > 0)
        {
            GachaPullResult result = queue.Dequeue();

            if (revealUI == null)
            {
                Debug.LogError("GachaSequencePlayer 缺少 GachaRevealUI。");
                break;
            }

            bool finished = false;

            revealUI.Reveal(result, () =>
            {
                finished = true;
            });

            while (!finished)
                yield return null;

            revealUI.Hide();

            // 两张角色揭示之间留出短暂间隔
            yield return new WaitForSeconds(0.12f);
        }

        FinishSequence();
    }

    public void SkipAll()
    {
        if (!isPlaying)
            return;

        queue.Clear();

        if (revealUI != null)
            revealUI.Hide();

        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }

        FinishSequence();
    }

    private void FinishSequence()
    {
        isPlaying = false;
        sequenceCoroutine = null;

        Action callback = onSequenceCompleted;
        onSequenceCompleted = null;

        callback?.Invoke();
    }
}
