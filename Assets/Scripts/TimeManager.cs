using System;
using System.Collections;
using UnityEngine;

public static class TimeManager
{
    public static event Action<float> OnTimeScaleChange;

    static Coroutine hitStopRoutine;
    static float defaultFixedDelta = -1f;

    static float gameplayScale = 1f;
    static int freezeCount;

    static bool isLocked;

    public static float TimeScale => Time.timeScale;
    public static bool IsFrozen => freezeCount > 0;

    static void Apply()
    {
        if (defaultFixedDelta < 0f) defaultFixedDelta = Time.fixedDeltaTime;

        float scale = freezeCount > 0 ? 0f : gameplayScale;

        Time.timeScale = scale;
        if (scale > 0f) Time.fixedDeltaTime = defaultFixedDelta * scale;

        OnTimeScaleChange?.Invoke(scale);
    }
    
    public static void StartHitStop(float duration, float slowdownFactor, float recoverTime = 0.1f)
    {
        if (isLocked) return;
        RunHitStop(duration, slowdownFactor, recoverTime);
    }
    
    public static void LockedHitStop(float duration, float slowdownFactor, float recoverTime = 0.1f)
    {
        isLocked = true;
        RunHitStop(duration, slowdownFactor, recoverTime);
    }

    static void RunHitStop(float duration, float slowdownFactor, float recoverTime)
    {
        MonoBehaviour context = App.Instance;
        if (context == null) return;

        if (hitStopRoutine != null) context.StopCoroutine(hitStopRoutine);
        hitStopRoutine = context.StartCoroutine(HitStop(duration, slowdownFactor, recoverTime));
    }

    public static void StopHitStop()
    {
        if (isLocked) return;

        if (hitStopRoutine != null)
        {
            App.Instance.StopCoroutine(hitStopRoutine);
            hitStopRoutine = null;
        }
        gameplayScale = 1f;
        Apply();
    }

    static IEnumerator HitStop(float duration, float slowdownFactor, float recoverTime)
    {
        gameplayScale = slowdownFactor;
        Apply();

        yield return new WaitForSecondsRealtime(duration);

        if (recoverTime > 0f)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / recoverTime;
                gameplayScale = Mathf.Lerp(slowdownFactor, 1f, t);
                Apply();
                yield return null;
            }
        }

        gameplayScale = 1f;
        hitStopRoutine = null;
        isLocked = false;
        Apply();
    }

    public static void FreezeUnFreeze(bool isFreeze)
    {
        if (isFreeze) FreezGame();
        else UnFreeze();
    }

    public static void FreezGame()
    {
        if (isLocked) return;
        freezeCount++;
        Apply();
    }

    public static void UnFreeze()
    {
        if (isLocked) return;
        freezeCount = Mathf.Max(0, freezeCount - 1);
        Apply();
    }
}