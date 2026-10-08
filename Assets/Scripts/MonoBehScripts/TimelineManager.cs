using UnityEngine;
using UnityEngine.Playables;

public class TimelineManager : BoolStateObject
{
    [Header("Timeline")]
    [SerializeField] private PlayableDirector _director;

    [Header("Events")]
    [SerializeField] private BetterEvent _onStart;
    [SerializeField] private BetterEvent _onComplete;
    [SerializeField] private BetterEvent _onSkip;

    public bool PlayOnStart;

    protected override void Start()
    {
        base.Start();

        if (!IsUsed && PlayOnStart)
            Play();
    }

    private void OnEnable()
    {
        _director.stopped += OnDirectorStopped;
    }

    private void OnDisable()
    {
        _director.stopped -= OnDirectorStopped;
    }

    protected override void OnLoaded() => Skip();

    public void Play()
    {
        _director.Play();
        _onStart.Invoke();
    }

    public void Stop()
    {
        _director.Stop();
    }

    public void Skip()
    {
        _director.time = _director.duration;
        _director.Evaluate();
        _director.Stop();
        _onSkip.Invoke();
    }

    private void OnDirectorStopped(PlayableDirector director)
    {
        _onComplete.Invoke();
        Save(true);
    }
}