using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TransitionEffect : MonoBehaviour
{
    public static TransitionEffect Instance;
    public Image transitionImage;

    public bool IsBlending { get; private set; }

    private Coroutine currentRoutine;

    private Material runtimeMaterial;


    private void Awake()
    {
        if (Instance == null)
            Instance = this;

        transitionImage ??= GetComponent<Image>();

        runtimeMaterial = new Material(transitionImage.material);
        transitionImage.material = runtimeMaterial;
    }

    #region Public API

    public void BlendIn(float duration = 1f)
    {
        StartBlend(0f, 1f, duration);
    }

    public void BlendOut(float duration = 1f)
    {
        StartBlend(1f, 0f, duration);
    }


    public IEnumerator BlendInCoroutine(float duration = 1f)
    {
        yield return BlendRoutine(0f, 1f, duration);
    }

    public IEnumerator BlendOutCoroutine(float duration = 1f)
    {
        yield return BlendRoutine(1f, 0f, duration);
    }

    #endregion

    private void StartBlend(float from, float to, float duration)
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(BlendRoutine(from, to, duration));
    }

    private IEnumerator BlendRoutine(float from, float to, float duration)
    {
        IsBlending = true;

        float time = 0f;

        transitionImage.material.SetFloat("_Blend", from);

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = time / duration;
            float value = Mathf.Lerp(from, to, t);

            transitionImage.material.SetFloat("_Blend", value);

            yield return null;
        }

        transitionImage.material.SetFloat("_Blend", to);

        IsBlending = false;
        currentRoutine = null;
    }

    private void OnDestroy()
    {
        Destroy(runtimeMaterial);
        Instance = null;
    }
}