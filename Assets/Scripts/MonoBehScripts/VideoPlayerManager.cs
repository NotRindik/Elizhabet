using System;
using UnityEngine;
using UnityEngine.Video;
using Sirenix.OdinInspector;

[RequireComponent(typeof(VideoPlayer))]
public class VideoPlayerManager : SerializedMonoBehaviour
{
    private VideoPlayer player;

    public bool playOnce = true;
    private bool played;

    public string localKey = "Video";

    private string Key => WorldKeyBuilder.Build(this, localKey);

    public BetterEvent onEnd;
    public BetterEvent onStart;

    private VideoPlayer.EventHandler EndHandle;
    private VideoPlayer.EventHandler StartHandle;

    private void Awake()
    {
        player ??= GetComponent<VideoPlayer>();

        EndHandle = c => onEnd.Invoke();
    }

    private void OnEnable()
    {
        player.loopPointReached += EndHandle;
        player.started += StartHandle;
    }

    private void Start()
    {
        var global = SaveManager.Instance.GetModule<GlobalSaves>();

        bool exist = global.Exist(Key);
        if (exist)
            played = global.GetData(Key) == "1";
        else
            played = false;

        if (playOnce)
        {
            if (!played)
            {
                Play();
                global.SetData(Key, "1").Save();
            }
        }
        else
        {
            Play();
        }
    }
    private void Play()
    {
        player.time = 0;
        player.Play();
        onStart.Invoke();
    }

    private void OnDisable()
    {
        player.loopPointReached -= EndHandle;
        player.started -= StartHandle;
    }
}