using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    private const string SFX_PARENT_NAME = "SFX";
    private const string SFX_NAME_FORMAT = "SFX - [{0}]";

    public const float TRACK_TRANSITION_SPEED = 1f;
    public static AudioManager instance { get; private set; }

    private Dictionary<int, AudioChannel> _channels = new Dictionary<int, AudioChannel>();
    private List<AudioSource> _soundEffects = new List<AudioSource>();

    public AudioMixerGroup musicMixer;
    public AudioMixerGroup sfxMixer;
    public AudioMixerGroup voicesMixer;

    private Transform sfxRoot;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

        }
        else if (instance != this)
        {
            Destroy(instance.gameObject);
            instance = this;
        }
        TimeManager.OnTimeScaleChange += OnTimeScaleChange;

        sfxRoot = new GameObject(SFX_PARENT_NAME).transform;
        sfxRoot.SetParent(transform);
    }
    private void OnTimeScaleChange(float time)
    {
        CleanAudioEffects();
        foreach (var soundEffect in _soundEffects)
        {
            soundEffect.pitch = time;
        }
        foreach(var channel in _channels.Values)
        {
            if (channel != null && channel.activeTrack != null)
            {
                channel.activeTrack.pitch = time;
            }
        }
    }

    public void PlayAudioClip(AudioClip audioClip)
    {
        PlaySoundEffect(audioClip);
    }

    public void PlayMusic(AudioClip audioClip)
    {
        PlayTrack(audioClip);
    }
    public void StopMusic(AudioClip audioClip)
    {
        string audioName = audioClip.name;
        StopTrack(audioName);
    }
    public void StopMusic(int channel)
    {
        StopTrack(channel);
    }
    public void StopMusic(string audioName)
    {
        StopTrack(audioName);
    }
    public AudioSource PlaySoundEffect(string filepath,AudioMixerGroup mixer = null,float volume = 1,float pitch = 1,bool loop = false)
    {
        AudioClip clip = Resources.Load<AudioClip>(filepath);

        if (clip == null)
        {
            Debug.LogError($"Could not load audio file '{filepath}'. Please make sure this exist audio");
            return null;
        }

        return PlaySoundEffect(clip,mixer,volume,pitch,loop);
    }

    public AudioSource PlaySoundEffect(AudioClip clip, AudioMixerGroup mixer = null, float volume = 1, float pitch = 1, bool loop = false)
    {
        CleanAudioEffects();
        AudioSource effectSource = new GameObject(string.Format(SFX_NAME_FORMAT, clip.name)).AddComponent<AudioSource>();

        effectSource.transform.SetParent(sfxRoot);
        effectSource.transform.position = sfxRoot.position;

        effectSource.clip = clip;

        if (mixer == null)
            mixer = sfxMixer;

        effectSource.outputAudioMixerGroup = mixer;
        effectSource.volume = volume;
        effectSource.spatialBlend = 0;
        effectSource.pitch = pitch;
        effectSource.loop = loop;

        effectSource.Play();
        _soundEffects.Add(effectSource);
        if (!loop)
            Destroy(effectSource.gameObject,(clip.length / pitch) + 1);

        return effectSource;
    }

    public AudioSource PlayVoice(string filepath, float volume = 1, float pitch = 1, bool loop = false)
    {
        return PlaySoundEffect(filepath, voicesMixer, volume, pitch, loop);
    }
    public AudioSource PlayVoice(AudioClip clip, float volume = 1, float pitch = 1, bool loop = false)
    {
        return PlaySoundEffect(clip, voicesMixer, volume, pitch, loop);
    }

    public void StopSoundEffect(AudioClip clip) => StopSoundEffect(clip.name);

    public void StopSoundEffect(string soundName)
    {
        CleanAudioEffects();
        soundName = soundName.ToLower();
        
        foreach (var source in _soundEffects)
        {
            if (source.clip.name.ToLower() == soundName)
            {
                Destroy(source.gameObject);
                CleanAudioEffects();
                return;
            }
        }
    }

    public void StopSoundEffect(AudioSource s)
    {
        CleanAudioEffects();

        foreach (var source in _soundEffects)
        {
            if (source == s)
            {
                Destroy(source.gameObject);
                CleanAudioEffects();
                return;
            }
        }
    }
    private void CleanAudioEffects()
    {
        _soundEffects.RemoveAll(c => c == null);
    }
    public AudioTrack PlayTrack(string filePath, int channel = 0, bool loop = true,float startingVolume = 0f,float volumeCap = 1f,float pitch = 1f)
    {
        AudioClip clip = Resources.Load<AudioClip>(filePath);

        if(clip == null)
        {
            Debug.LogError($"Could not load audio file '{filePath}'. Please make sure this exists in the Resources directory");
            return null;
        }

        return PlayTrack(clip, channel, loop, startingVolume, volumeCap,pitch, filePath);
    }

    public AudioTrack PlayTrack(AudioClip clip, int channel = 0, bool loop = true, float startingVolume = 0f, float volumeCap = 1f,float pitch = 1f,string filePath = "")
    {
        CleanAudioEffects();
        AudioChannel audioChannel = TryGetChannel(channel, createIfNotExists:true);
        AudioTrack track = audioChannel.PlayTrack(clip, loop, startingVolume, volumeCap, pitch, filePath);
        return track;
    }

    public void StopTrack(int channel)
    {
        AudioChannel c = TryGetChannel(channel, createIfNotExists: false);

        if (c == null)
            return;

        c.StopTrack();
    }
    public void StopTrack(string trackName)
    {
        trackName = trackName.ToLower();

        foreach(var channel in _channels.Values)
        {
            if (channel.activeTrack != null &&  channel.activeTrack.name.ToLower() == trackName)
            {
                channel.StopTrack();
                return;
            }
        }
    }

    public AudioChannel TryGetChannel(int channelNumber,bool createIfNotExists = false)
    {
        AudioChannel channel = null;

        if (_channels.TryGetValue(channelNumber, out channel))
        {
            return channel;
        }
        else if (createIfNotExists)
        {
            channel = new AudioChannel(channelNumber);
            _channels.Add(channelNumber, channel);
            return channel;
        }
        return null;
    }
}
