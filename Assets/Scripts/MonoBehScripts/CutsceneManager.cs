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

    public Action<PlayableDirector> Endhandle;

    private void Awake()
    {
        director ??= GetComponent<PlayableDirector>();
        Endhandle = c => onEnd.Invoke();
    }
    private void OnEnable()
    {
        director.stopped += Endhandle;
    }

    public void Start()
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
                director.Play();
                global.SetData(Key, "1").Save();

            }
        }
        else
        {
            director.Play();
        }
    }

    private void OnDisable()
    {
        director.stopped -= Endhandle;
    }
}