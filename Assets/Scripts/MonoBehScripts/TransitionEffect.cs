using System;
using Assets.Scripts;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TransitionEffect : MonoBehaviour, IGameService
{
    public static TransitionEffect Instance;
    public Image transitionImage;

    public bool IsBlending { get; private set; }

    private Coroutine currentRoutine;

    private Material runtimeMaterial;


    #region Public API

    public void BlendIn(float duration = 1f,string effectName = "",Action onFinish = null)
    {
        StartBlend(0f, 1f, duration,effectName,onFinish);
    }

    public void BlendOut(float duration = 1f, string effectName = "",Action onFinish = null)
    {
        StartBlend(1f, 0f, duration,effectName,onFinish);
    }


    public IEnumerator BlendInCoroutine(float duration = 1f, string effectName = "",Action onFinish = null)
    {
        yield return BlendRoutine(0f, 1f, duration,effectName,onFinish);
    }

    public IEnumerator BlendOutCoroutine(float duration = 1f, string effectName = "",Action onFinish = null)
    {
        yield return BlendRoutine(1f, 0f, duration, effectName,onFinish);
    }

    #endregion

    private void StartBlend(float from, float to, float duration, string effectName = "",Action onFinish = null)
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(BlendRoutine(from, to, duration, effectName,onFinish));
    }

    private IEnumerator BlendRoutine(float from, float to, float duration, string effectName = "", Action onFinish = null)
    {
        IsBlending = true;

        float time = 0f;
        if (!string.IsNullOrEmpty(effectName))
        {
            var texture = Resources.Load<Texture>($"{FileManager.TransitionEffects}{effectName}");
            if(texture) 
                transitionImage.material.SetTexture("_BlendTex", texture);
        }

        transitionImage.material.SetFloat("_Blend", from);

        while (time < duration)
        {
            time += Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.05f);
            float t = time / duration;
            float value = Mathf.Lerp(from, to, t);

            transitionImage.material.SetFloat("_Blend", value);

            yield return null;
        }

        transitionImage.material.SetFloat("_Blend", to);

        IsBlending = false;
        currentRoutine = null;
        onFinish?.Invoke();
    }

    private void OnDestroy()
    {
        Destroy(runtimeMaterial);
        Instance = null;
    }

    public void Init()
    {
        if (Instance == null)
            Instance = this;

        transitionImage ??= GetComponent<Image>();

        runtimeMaterial = new Material(transitionImage.material);
        transitionImage.material = runtimeMaterial;
    }
}