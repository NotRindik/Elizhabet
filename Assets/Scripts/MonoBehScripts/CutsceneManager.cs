using System;
using UnityEngine;
using UnityEngine.Playables;

[RequireComponent(typeof(PlayableDirector))]
public class CutsceneManager : MonoBehaviour
{
    private PlayableDirector director;

    public bool playOnce = true;
    private bool played;

    public string localKey = "CutScene";

    private string Key => WorldKeyBuilder.Build(this,localKey);

    public BetterEvent onEnd;
    public BetterEvent onStart;

    public Action<PlayableDirector> Endhandle;
    public Action<PlayableDirector> StartHandle;

    public bool playOnStart = true;

    private void Awake()
    {
        director ??= GetComponent<PlayableDirector>();
        Endhandle = c => onEnd.Invoke();
        StartHandle = c => onStart.Invoke();
    }
    private void OnEnable()
    {
        director.stopped += Endhandle;
        director.played += StartHandle;
    }

    public void Start()
    {
        var global = SaveManager.Instance.GetModule<WorldScope>();
        played = global.HasFlag(Key);

        if (!playOnStart)
            return;

        if (playOnce)
            Play(global);
        else
            director.Play();
    }


    private void Play(WorldScope global)
    {
        if (played)
            return;

        played = true;
        global.SetFlag(Key);

        director.Play();

        SaveManager.Instance.SaveAsync();
    }

    private void OnDisable()
    {
        director.stopped -= Endhandle;
        director.played -= StartHandle;
    }
}