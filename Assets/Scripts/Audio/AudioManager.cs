using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;



public class AudioManager : MonoBehaviour, IGameService
{
    private struct ActiveSound
    {
        public AudioSource source;
        public float endTime;
    }
    
    private const string SFX_PARENT_NAME = "SFX";
    private const string SFX_NAME_FORMAT = "SFX - [{0}]";

    public const float TRACK_TRANSITION_SPEED = 1f;
    public static AudioManager instance { get; private set; }

    private Dictionary<int, AudioChannel> _channels = new Dictionary<int, AudioChannel>();
    
    private List<ActiveSound> _timed = new List<ActiveSound>();
    
    private Queue<AudioSource> _pool = new Queue<AudioSource>();
    private List<AudioSource> _active = new List<AudioSource>();

    [SerializeField] private int initialPoolSize = 10;

    public AudioMixerGroup musicMixer;
    public AudioMixerGroup sfxMixer;
    public AudioMixerGroup voicesMixer;

    private Transform sfxRoot;
    public void Init()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(instance.gameObject);
            instance = this;
        }
        
        
        TimeManager.OnTimeScaleChange += OnTimeScaleChange;

        sfxRoot = new GameObject(SFX_PARENT_NAME).transform;
        sfxRoot.SetParent(transform);
        
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewSource();
        }
    }
    
    private AudioSource CreateNewSource()
    {
        GameObject go = new GameObject("SFX_Pooled");
        go.transform.SetParent(sfxRoot);

        AudioSource src = go.AddComponent<AudioSource>();
        go.SetActive(false);

        _pool.Enqueue(src);
        return src;
    }
    
    private AudioSource GetSource()
    {
        if (_pool.Count == 0)
            CreateNewSource();

        AudioSource src = _pool.Dequeue();
        src.gameObject.SetActive(true);
        _active.Add(src);

        return src;
    }
    
    void Update()
    {
        float time = Time.time;

        for (int i = _timed.Count - 1; i >= 0; i--)
        {
            if (time >= _timed[i].endTime)
            {
                ReturnSource(_timed[i].source);
                _timed.RemoveAt(i);
            }
        }
    }
    private void OnTimeScaleChange(float time)
    {
        for (int i = _timed.Count - 1; i >= 0; i--)
        {
            var src = _timed[i].source;

            if (src == null)
            {
                _timed.RemoveAt(i);
                continue;
            }

            src.pitch = time;
        }

        foreach (var channel in _channels.Values)
        {
            if (channel != null && channel.activeTrack != null && channel.activeTrack != null)
            {
                channel.activeTrack.pitch = time;
            }
        }
    }

    public void PlayAudioClip(AudioClip audioClip)
    {
        PlaySoundEffect(audioClip);
    }

    public AudioSource PlayEvent(EventSoundInstance @event)
    {
        @event.Init();
        
        AudioSource effectSource = GetSource();

        if (@event.clip == null)
            return null;

        effectSource.transform.SetParent(sfxRoot);
        effectSource.transform.position = sfxRoot.position;

        effectSource.clip = @event.clip;

        if (@event.mixer == null)
            @event.mixer = sfxMixer;

        effectSource.outputAudioMixerGroup = @event.mixer;
        effectSource.volume = @event.volume;
        effectSource.spatialBlend = 0;
        effectSource.pitch = @event.pitch;

        effectSource.Play();
        
        _timed.Add(new ActiveSound
        {
            source = effectSource,
            endTime = Time.time + (@event.clip.length / @event.pitch)
        });

        return effectSource;
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
    private void ReturnSource(AudioSource src)
    {
        if (src == null) return;

        src.Stop();
        src.clip = null;
        src.loop = false;

        src.gameObject.SetActive(false);

        _active.Remove(src);
        _pool.Enqueue(src);
    }
    public AudioSource PlaySoundEffect(AudioClip clip, AudioMixerGroup mixer = null, float volume = 1, float pitch = 1, bool loop = false)
    {
        AudioSource effectSource = GetSource();

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
        {
            _timed.Add(new ActiveSound
            {
                source = effectSource,
                endTime = Time.time + (clip.length / pitch)
            });
        }

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

    public void StopSoundEffect(AudioClip clip)
    {
        if (clip == null) return;

        StopSoundEffect(clip.name);
    }
    public void StopAllSFX()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            ReturnSource(_active[i]);
        }

        _timed.Clear();
    }
    public void StopSoundEffect(string soundName)
    {
        if (string.IsNullOrEmpty(soundName)) return;

        soundName = soundName.ToLower();

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var src = _active[i];

            if (src.clip != null && src.clip.name.ToLower() == soundName)
            {
                RemoveFromTimed(src);
                ReturnSource(src);
                return;
            }
        }
    }

    public void StopSoundEffect(AudioSource s)
    {
        if (s == null) return;

        RemoveFromTimed(s);
        ReturnSource(s);
    }
    
    
    private void RemoveFromTimed(AudioSource src)
    {
        for (int i = _timed.Count - 1; i >= 0; i--)
        {
            if (_timed[i].source == src)
            {
                _timed.RemoveAt(i);
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
