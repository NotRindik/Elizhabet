using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    private const string SFX_PARENT_NAME = "SFX";
    private const string SFX_NAME_FORMAT = "SFX - [{0}]";

    public const float TRACK_TRANSITION_SPEED = 1f;
    public static AudioManager instance { get; private set; }

    public Dictionary<int, AudioChannel> channels = new Dictionary<int, AudioChannel>();

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

        sfxRoot = new GameObject(SFX_PARENT_NAME).transform;
        sfxRoot.SetParent(transform);
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
        soundName = soundName.ToLower();

        AudioSource[] sources = sfxRoot.GetComponentsInChildren<AudioSource>();
        foreach (var source in sources)
        {
            if (source.clip.name.ToLower() == soundName)
            {
                Destroy(source.gameObject);
                return;
            }
        }
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

        foreach(var channel in channels.Values)
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

        if (channels.TryGetValue(channelNumber, out channel))
        {
            return channel;
        }
        else if (createIfNotExists)
        {
            channel = new AudioChannel(channelNumber);
            channels.Add(channelNumber, channel);
            return channel;
        }
        return null;
    }
}
