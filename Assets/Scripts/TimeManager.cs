using System;
using System.Collections;
using UnityEngine;

public static class TimeManager
{
    private static Coroutine hitStopRoutine;
    public static event Action<float> OnTimeScaleChange;

    public static float TimeScale
    {
        get
        {
            return Time.timeScale;
        }
        set
        {
            Time.timeScale = value;
            OnTimeScaleChange?.Invoke(value);
        }
    }

    public static void StartHitStop(float duration,float maxDuration, float slowdownFactor, MonoBehaviour context)
    {
        if (hitStopRoutine != null)
            context.StopCoroutine(hitStopRoutine);

        hitStopRoutine = context.StartCoroutine(HitStop(duration,maxDuration ,slowdownFactor));
    }

    private static IEnumerator HitStop(float duration, float maxDuration, float slowdownFactor)
    {
        TimeScale = slowdownFactor;
        yield return new WaitForSecondsRealtime(Mathf.Min(duration, maxDuration));
    
        float t = 0f;
        float smoothTime = 0.1f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / smoothTime;
            TimeScale = Mathf.Lerp(slowdownFactor, 1f, t);
            yield return null;
        }
        TimeScale = 1f;
        hitStopRoutine = null;
    }
}
