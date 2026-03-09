using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class UITransitionWrapper : MonoBehaviour
{
    private CanvasGroup cg;

    [Header("Settings")]
    public float fadeDuration = 0.25f;
    public bool disableInteractionWhenHidden = true;
    public bool disableRaycastWhenHidden = true;

    Coroutine fadeRoutine;

    void Awake()
    {
        if (!cg)
            cg = GetComponent<CanvasGroup>();
    }

    public void Show(bool instant = false)
    {
        SetState(true, instant);
    }

    public void Hide(bool instant = false)
    {
        SetState(false, instant);
    }

    void SetState(bool visible, bool instant)
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        if (instant)
        {
            cg.alpha = visible ? 1 : 0;
            UpdateInteraction(visible);
            return;
        }

        fadeRoutine = StartCoroutine(FadeRoutine(visible));
    }

    IEnumerator FadeRoutine(bool visible)
    {
        float start = cg.alpha;
        float target = visible ? 1f : 0f;

        float t = 0;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(start, target, t / fadeDuration);
            yield return null;
        }

        cg.alpha = target;

        UpdateInteraction(visible);
        fadeRoutine = null;
    }

    void UpdateInteraction(bool visible)
    {
        if (disableInteractionWhenHidden)
            cg.interactable = visible;

        if (disableRaycastWhenHidden)
            cg.blocksRaycasts = visible;
    }
}