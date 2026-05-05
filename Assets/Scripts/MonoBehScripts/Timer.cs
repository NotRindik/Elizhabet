using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

[System.Serializable]
public struct TimerEvent
{
    public BetterEvent betterEvent;
    public float invokeTime;
}

public class Timer : SerializedMonoBehaviour
{
    public TimerEvent[] timers;
    public bool invokeInStart;

    [ShowInInspector, ReadOnly] private float _elapsed;
    [ShowInInspector, ReadOnly] private bool _running;

    private Coroutine _coroutine;
    private bool[] _invoked;

    private void Start()
    {
        if (invokeInStart)
            StartTimer();
    }

    public void StartTimer()
    {
        if (_coroutine != null)
            StopCoroutine(_coroutine);

        _elapsed = 0f;
        _invoked = new bool[timers.Length];
        _running = true;
        _coroutine = StartCoroutine(TimerRoutine());
    }

    public void StopTimer()
    {
        if (_coroutine != null)
        {
            StopCoroutine(_coroutine);
            _coroutine = null;
        }
        _running = false;
    }

    public void ResetTimer()
    {
        StopTimer();
        _elapsed = 0f;
        _invoked = new bool[timers.Length];
    }

    private IEnumerator TimerRoutine()
    {
        while (true)
        {
            _elapsed += Time.deltaTime;

            for (int i = 0; i < timers.Length; i++)
            {
                if (!_invoked[i] && _elapsed >= timers[i].invokeTime)
                {
                    timers[i].betterEvent.Invoke();
                    _invoked[i] = true;
                }
            }
            
            bool allDone = true;
            for (int i = 0; i < _invoked.Length; i++)
                if (!_invoked[i]) { allDone = false; break; }

            if (allDone)
            {
                _running = false;
                yield break;
            }

            yield return null;
        }
    }
}