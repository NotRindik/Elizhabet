using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(AudioSource))]
public class AudioEventer : MonoBehaviour
{
    [SerializeField] private List<AudioEvent> events = new();

    [SerializeField] private AudioSource source;

    private float prevNormalizedTime;
    private int lastSample;

    private void Awake()
    {
        source ??= GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        ResetEvents();
    }

    private void Update()
    {
        if (!source.isPlaying || source.clip == null)
            return;

        float normalizedTime = GetNormalizedTime();

        if (IsLooped(normalizedTime))
        {
            ResetEvents();
        }
        
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];

            if (!e.fired && normalizedTime >= e.normalizedTime)
            {
                e.fired = true;
                e.onEvent?.Invoke();
            }
        }

        prevNormalizedTime = normalizedTime;
        lastSample = source.timeSamples;
    }

    private float GetNormalizedTime()
    {
        if (source.clip == null || source.clip.samples == 0)
            return 0f;

        return (float)source.timeSamples / source.clip.samples;
    }

    private bool IsLooped(float currentNormalized)
    {
        return currentNormalized < prevNormalizedTime || source.timeSamples < lastSample;
    }

    private void ResetEvents()
    {
        for (int i = 0; i < events.Count; i++)
        {
            events[i].fired = false;
        }
    }
}

[Serializable]  
public class AudioEvent
{
    [Range(0f, 1f)]
    public float normalizedTime;

    public UnityEvent onEvent;

    [NonSerialized] public bool fired;
}