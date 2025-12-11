using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(fileName = "EventSound", menuName = "AudioEvents")]
public class EventSound : ScriptableObject
{
    public AudioClip[] clipSequence;
    [SerializeReference,SubclassSelector] public EventMod[] mods;
}

public unsafe interface EventMod
{
    public void Execute(EventSoundInstance @event);
}

[System.Serializable]   
public class EventSoundInstance
{
    public EventSound asset;
    public AudioClip clip;
    public AudioMixerGroup mixer;
    public float pitch,volume;

    public EventSoundInstance(EventSound asset)
    {
        this.asset = asset;
        pitch = 1f;
        volume = 1f;
        clip = null;
        mixer = null;
    }

    public EventSoundInstance()
    {
        this.asset = null;
        pitch = 1f;
        volume = 1f;
        clip = null;
        mixer = null;
    }

    public AudioClip Init()
    {
        foreach (var mod in asset.mods)
            mod.Execute(this);

        return clip;
    }
}


[System.Serializable]
public class PlayIndex : EventMod
{
    public int index;
    public void Execute(EventSoundInstance @event)
    {
        @event.clip = @event.asset.clipSequence[index];
    }
}
[System.Serializable]
public class RandomIndex : EventMod
{
    public void Execute(EventSoundInstance e)
    {
        int index = Random.Range(0, e.asset.clipSequence.Length);
        e.clip = e.asset.clipSequence[index];
    }
}
[System.Serializable]
public class PitchRange : EventMod
{
    [MinMaxSlider(-3,3)]
    public Vector2 pitch;

    public void Execute(EventSoundInstance e)
    {
        e.pitch = Random.Range(pitch.x, pitch.y);
    }
}

[System.Serializable]
public class MixClips : EventMod
{
    public int[] mixClips;
    public void Execute(EventSoundInstance e)
    {
        var sequence = e.asset.clipSequence;
        if (mixClips.Length == 1)
        {
            e.clip = sequence[mixClips[0]];
            return;
        }

        var clips = new AudioClip[mixClips.Length];
        for (int i = 0; i < mixClips.Length; i++)
        {
            int id = mixClips[i];
            if (id < 0 || id >= sequence.Length)
                continue;
            clips[i] = sequence[mixClips[i]];
        }
        e.clip = AudioMixerUtility.Mix(clips);
    }
}


public class AudioMixerUtility
{
    public static AudioClip Mix(AudioClip a, AudioClip b)
    {
        int channels = a.channels;
        int frequency = a.frequency;
        int length = Mathf.Max(a.samples, b.samples);

        float[] dataA = new float[a.samples * channels];
        float[] dataB = new float[b.samples * channels];
        float[] result = new float[length * channels];

        a.GetData(dataA, 0);
        b.GetData(dataB, 0);

        for (int i = 0; i < result.Length; i++)
        {
            float sA = i < dataA.Length ? dataA[i] : 0f;
            float sB = i < dataB.Length ? dataB[i] : 0f;

            // простой микс
            result[i] = Mathf.Clamp(sA + sB, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Mixed", length, channels, frequency, false);
        clip.SetData(result, 0);

        return clip;
    }

    public static AudioClip Mix(params AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        if (clips.Length == 1)
            return clips[0];

        int channels = clips[0].channels;
        int frequency = clips[0].frequency;

        foreach (AudioClip c in clips)
        {
            if (c.channels != channels || c.frequency != frequency)
                throw new System.Exception("¬се клипы должны иметь одинаковые channels и frequency!");
        }

        int maxSamples = 0;
        foreach (AudioClip c in clips)
            if (c.samples > maxSamples)
                maxSamples = c.samples;

        float[] result = new float[maxSamples * channels];

        float[] temp = null;

        foreach (AudioClip c in clips)
        {
            int sampleCount = c.samples * channels;

            if (temp == null || temp.Length < sampleCount)
                temp = new float[sampleCount];

            c.GetData(temp, 0);

            for (int i = 0; i < sampleCount; i++)
            {
                result[i] += temp[i];
            }
        }

        for (int i = 0; i < result.Length; i++)
        {
            if (result[i] > 1f) result[i] = 1f;
            else if (result[i] < -1f) result[i] = -1f;
        }

        AudioClip mixed = AudioClip.Create(
            "Mixed_" + clips.Length,
            maxSamples,
            channels,
            frequency,
            false
        );

        mixed.SetData(result, 0);
        return mixed;
    }
}