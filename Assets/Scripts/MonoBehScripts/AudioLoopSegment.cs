using UnityEngine;
using Sirenix.OdinInspector;

[ExecuteAlways]
[RequireComponent(typeof(AudioSource))]
public class AudioLoopSegment : MonoBehaviour
{
    [Title("Source")]
    [Required, HideLabel]
    public AudioSource source;

    // ===================== INFO =====================

    [FoldoutGroup("Clip Info"), ReadOnly]
    public AudioClip clip;

    [FoldoutGroup("Clip Info"), ReadOnly]
    public float clipLength;

    [FoldoutGroup("Clip Info"), ReadOnly]
    public int frequency;

    [FoldoutGroup("Clip Info"), ReadOnly]
    public int totalSamples;

    // ===================== LOOP =====================

    [Title("Loop Settings")]

    [MinValue(0)]
    [MaxValue("@clipLength")]
    [OnValueChanged(nameof(OnTimeChanged))]
    public float loopStartTime;

    [MinValue(0)]
    [MaxValue("@clipLength")]
    [OnValueChanged(nameof(OnTimeChanged))]
    public float loopEndTime = 1f;

    [ShowInInspector, ReadOnly]
    public int loopStartSample;

    [ShowInInspector, ReadOnly]
    public int loopEndSample;

    // ===================== DEBUG =====================

    [Title("Debug")]
    [ShowInInspector, ReadOnly]
    public int currentSample;

    // ===================== INIT =====================

    private void OnEnable()
    {
        TryInit();
    }

    private void OnValidate()
    {
        TryInit();
        ClampValues();
        UpdateSamples();
    }

    private void TryInit()
    {
        if (source == null)
            source = GetComponent<AudioSource>();

        if (source == null) return;

        if (clip != source.clip)
        {
            clip = source.clip;
            UpdateClipInfo();
        }
    }

    private void UpdateClipInfo()
    {
        if (clip == null) return;

        clipLength = clip.length;
        frequency = clip.frequency;
        totalSamples = clip.samples;
    }

    // ===================== LOGIC =====================

    private void OnTimeChanged()
    {
        ClampValues();
        UpdateSamples();
    }

    private void ClampValues()
    {
        if (clip == null) return;

        loopStartTime = Mathf.Clamp(loopStartTime, 0, clipLength);
        loopEndTime = Mathf.Clamp(loopEndTime, 0, clipLength);

        // важный момент 👇
        if (loopEndTime < loopStartTime)
            loopEndTime = loopStartTime;
    }

    private void UpdateSamples()
    {
        if (clip == null) return;

        loopStartSample = (int)(loopStartTime * frequency);
        loopEndSample = (int)(loopEndTime * frequency);
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (source == null || !source.isPlaying) return;

        currentSample = source.timeSamples;

        if (currentSample >= loopEndSample)
        {
            source.timeSamples = loopStartSample;
        }
    }

    // ===================== CONTROLS =====================

    [Button(ButtonSizes.Large)]
    public void PlayFromLoopStart()
    {
        if (clip == null) return;

        source.loop = false;
        source.timeSamples = loopStartSample;
        source.Play();
    }

    [Button(ButtonSizes.Large)]
    public void PlayFromClipStartThenLoop()
    {
        if (clip == null) return;

        source.loop = false;
        source.timeSamples = 0;
        source.Play();
    }

    [Button]
    public void Stop()
    {
        source.Stop();
    }

    [Button]
    public void PreviewInEditor()
    {
#if UNITY_EDITOR
        if (clip == null) return;

        UnityEditor.EditorApplication.update -= EditorPreviewUpdate;
        source.timeSamples = loopStartSample;
        source.Play();

        UnityEditor.EditorApplication.update += EditorPreviewUpdate;
#endif
    }

#if UNITY_EDITOR
    private void EditorPreviewUpdate()
    {
        if (source == null || !source.isPlaying)
        {
            UnityEditor.EditorApplication.update -= EditorPreviewUpdate;
            return;
        }

        if (source.timeSamples >= loopEndSample)
        {
            source.timeSamples = loopStartSample;
        }
    }
#endif
}