using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.SceneManagement;

public interface IGameMode
{
    public IEnumerator OnEnd();
    public void OnEditorStart();
    public void OnEditorEnd();
    public IEnumerator OnStart();
}

public class GameModeManager : MonoBehaviour, IGameService
{
    private bool _isSwitching;

    private IGameMode _currenMode;

    public Action OnGameModeChange;

    public static GameModeManager Instance { get; private set; }

    public MainMenu mainMenuMode = new MainMenu();
    public StoryMode storyMode = new StoryMode();
    public static string InitialScene = "UI";
    private void Awake()
    {
#if UNITY_EDITOR
        switch (SceneManager.GetActiveScene().buildIndex)
        {
            case 0:
                HandleStartRequest(mainMenuMode);
                    break;
            case 1:
                _currenMode = mainMenuMode;
                _currenMode.OnEditorStart();
/*                SceneManager.LoadScene(InitialScene,LoadSceneMode.Additive);*/
                break;
            default:
                App.IsEditor = true;
                _currenMode = storyMode;
                _currenMode.OnEditorStart();
                SceneManager.LoadScene(InitialScene,LoadSceneMode.Additive);
                break;
        }
#else
        HandleStartRequest(mainMenuMode);
#endif
    }

    public void Init()
    {
        if(Instance == null)
            Instance = this;
    }

    private void OnDestroy()
    {
        Instance = null;
    }

    public void HandleStartRequest(IGameMode mode)
    {
        StartCoroutine(SwitchMode(mode));
    }

    private IEnumerator SwitchMode(IGameMode mode)
    {
        yield return new WaitUntil(() => !_isSwitching);

        if (_currenMode == mode)
            yield break;

        _isSwitching = true;
        //TODO сделать анимку перехода
        if (_currenMode != null)
            yield return _currenMode.OnEnd();
        _currenMode = mode;
        yield return _currenMode.OnStart();

        _isSwitching = false;
        OnGameModeChange?.Invoke();
    }
}

public class MainMenu : IGameMode
{
    public void OnEditorEnd()
    {
        throw new NotImplementedException();
    }

    public void OnEditorStart()
    {
        throw new NotImplementedException();
    }

    public IEnumerator OnEnd()
    {
        throw new NotImplementedException();
    }

    public IEnumerator OnStart()
    {
        throw new NotImplementedException();
    }
}

public class StoryMode : IGameMode
{
    public bool IsPaused { get; set; }
    private GameModeState _state = GameModeState.Ended;
    public IEnumerator OnEnd()
    {
        _state = GameModeState.Ended;
        yield break;
    }

    public void Setup()
    {

    }

    public IEnumerator OnStart()
    {
        if (_state != GameModeState.Ended) yield break;
        _state = GameModeState.Starting;

        var save = SaveManager.Instance;
        string sceneName = save.GetModule<SaveManifest>().saveManifest.sceneName;

        if (string.IsNullOrEmpty(sceneName))
        {
            string path = SceneUtility.GetScenePathByBuildIndex(1);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            sceneName = name;
        }

        yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));

        yield return SceneManager.LoadSceneAsync("UI", LoadSceneMode.Additive);

        IsPaused = false;
        SaveManager.Instance.Load();

        _state = GameModeState.Started;
    }

    public void Pause() 
    {
        IsPaused = true;
    }

    public void Resume() 
    {
        IsPaused = false;
    }

    public void OnEditorStart()
    {
#if UNITY_EDITOR
        
        _state = GameModeState.Started;
        Resume();
#endif
    }

    public void OnEditorEnd()
    {
        throw new NotImplementedException();
    }
}


public enum GameModeState
{
    None,Starting,Started,Ended,
}