using System;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using NaughtyAttributes;
using Sirenix.OdinInspector;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;
using ButtonAttribute = Sirenix.OdinInspector.ButtonAttribute;
using Random = UnityEngine.Random;

[CreateAssetMenu(fileName = "EventSound", menuName = "AudioEvents")]
public class EventSound : SerializedScriptableObject
{
    public AudioClip[] clipSequence;
    [SerializeReference,SubclassSelector] public EventMod[] mods;

    [Button]
    public void TestSound()
    {
        var instance = new EventSoundInstance(this);
        instance.Init();

        EditorAudioSource.Play(instance);
    }


    public T GetMode<T>()  where T : EventMod
    {
        for (int i = 0; i < mods.Length; i++)
        {
            if (mods[i] is T)
            {
                return (T)mods[i];
            }
        }

        return default;
    }
}

public interface EventMod
{
    public void Execute(EventSoundInstance @event);
}
public interface ISoundData { }

[System.Serializable]   
public class EventSoundInstance
{
    public EventSound asset;
    public AudioClip clip;
    public AudioMixerGroup mixer;
    public float pitch,volume;

    public AudioClip[] sequence;
    public ISoundData[] data;
    private Dictionary<Type, ISoundData> _data;
    
    public void SetData<T>(T data) where T : ISoundData
    {
        _data ??= new Dictionary<Type, ISoundData>();
        _data[typeof(T)] = data;
    }
    
    public void SetDataRange(params ISoundData[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            SetData(data[i]);
        }
    }

    public bool TryGetData<T>(out T data) where T : ISoundData
    {
        if (_data != null && _data.TryGetValue(typeof(T), out var d))
        {
            data = (T)d;
            return true;
        }

        data = default;
        return false;
    }
    public EventSoundInstance(EventSound asset,params ISoundData[] data)
    {
        this.asset = asset;
        this.data = data;
        pitch = 1f;
        volume = 1f;
        sequence = asset.clipSequence;
        clip = null;
        mixer = null;
        
        for (int i = 0; i < this.data.Length; i++)
        {
            SetData(data[i]);
        }
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
public class PlayOrdered : EventMod
{
    public int index;

    public void Execute(EventSoundInstance e)
    {
        e.clip = e.sequence[index++];

        if (index == e.sequence.Length)
            index = 0;
    }
}
public struct MaterialData : ISoundData
{
    public ObjectAudioMaterial material;
    public string interaction;
}
public class SoundByMaterial : EventMod
{
    public void Execute(EventSoundInstance e)
    {
        if (!e.TryGetData(out MaterialData md))
            return;

        if (md.material == null)
            return;

        e.sequence = md.material.GetSequence(md.interaction);
    }
}

[System.Serializable]
public class PlayIndex : EventMod
{
    [HideInInspector] public int index;
    public void Execute(EventSoundInstance e)
    {
        e.clip = e.sequence[index];
    }
}
[System.Serializable]
public class RandomIndex : EventMod
{
    public void Execute(EventSoundInstance e)
    {
        int index = Random.Range(0, e.sequence.Length);
        e.clip = e.sequence[index];
    }
}
[System.Serializable]
public class PitchRange : EventMod
{
    [NaughtyAttributes.MinMaxSlider(-3,3)]
    public Vector2 pitch;

    public void Execute(EventSoundInstance e)
    {
        e.pitch = Random.Range(pitch.x, pitch.y);
    }
}


[System.Serializable]
public class VolumeRange : EventMod
{
    [FormerlySerializedAs("pitch")] [NaughtyAttributes.MinMaxSlider(0,1)]
    public Vector2 vol;

    public void Execute(EventSoundInstance e)
    {
        e.volume = Random.Range(vol.x, vol.y);
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

            // ������� ����
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
                throw new System.Exception("��� ����� ������ ����� ���������� channels � frequency!");
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

public static class EditorAudioPlayer
{
#if UNITY_EDITOR
    public static void Play(AudioClip clip, float pitch = 1f)
    {
        if (clip == null) return;

        var audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");

        var method = audioUtil.GetMethod(
            "PlayClip",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new System.Type[] { typeof(AudioClip), typeof(int), typeof(bool), typeof(float) },
            null
        );

        method.Invoke(null, new object[] { clip, 0, false, pitch });
    }
#endif
}

public static class EditorAudioSource
{
    static AudioSource source;

    public static void Play(EventSoundInstance e)
    {
#if UNITY_EDITOR
        if (source == null)
        {
            var go = new GameObject("EditorAudioPreview");
            go.hideFlags = HideFlags.HideAndDontSave;
            source = go.AddComponent<AudioSource>();
        }

        source.clip = e.clip;
        source.pitch = e.pitch;
        source.volume = e.volume;
        source.outputAudioMixerGroup = e.mixer;

        source.Play();
#endif
    }
}