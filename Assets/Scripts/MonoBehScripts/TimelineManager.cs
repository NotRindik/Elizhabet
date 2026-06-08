using UnityEngine;
using UnityEngine.Playables;
using Sirenix.OdinInspector;

public class TimelineManager : MonoBehaviour
{
    [Header("Timeline")]
    [SerializeField] private PlayableDirector _director;

    [Header("Events")]
    [SerializeField] private BetterEvent _onStart;
    [SerializeField] private BetterEvent _onComplete;
    [SerializeField] private BetterEvent _onSkip;

    [Header("Save")]
    [SerializeField] private bool _saveState;
    [ShowIf(nameof(_saveState))]
    [SerializeField] private string _localKey = "played";

    private bool _isPlayed;

    private WorldObjectsStateSave WorldSave =>
        SaveManager.Instance.GetModule<WorldObjectsStateSave>();


    public bool PlayOnStart;

    private string SaveKey => WorldKeyBuilder.Build(this, _localKey);

    private void Start()
    {
        if(PlayOnStart)
            Play();
        
        
        if (!_saveState) return;

        if (WorldSave.Exist(SaveKey))
            _isPlayed = WorldSave.GetData(SaveKey) == "1";

        if (_isPlayed)
            Skip();
    }

    private void OnEnable()
    {
        _director.stopped += OnDirectorStopped;
    }

    private void OnDisable()
    {
        _director.stopped -= OnDirectorStopped;
    }

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

        if (!_saveState) return;

        _isPlayed = true;
        WorldSave.SetData(SaveKey, "1");
        SaveManager.Instance.SaveModule<WorldObjectsStateSave>();
    }
}