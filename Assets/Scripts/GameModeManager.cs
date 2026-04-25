using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.SceneManagement;
using static SceneLoader;
using Object = UnityEngine.Object;

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

    public IGameMode CurrMode => _currenMode;
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
                //SceneManager.LoadScene(InitialScene,LoadSceneMode.Additive);
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

        yield return TransitionEffect.Instance.BlendInCoroutine(0.5f,"blackHole");
        if (_currenMode != null)
            yield return _currenMode.OnEnd();
        _currenMode = mode;
        yield return _currenMode.OnStart();

        yield return TransitionEffect.Instance.BlendOutCoroutine(0.5f, "blackHole");
        _isSwitching = false;
        OnGameModeChange?.Invoke();
    }
}

public class MainMenu : IGameMode
{
    private GameModeState _state = GameModeState.Ended;
    public void OnEditorEnd()
    {
        SceneManager.UnloadSceneAsync("MainMenu");
    }

    public void OnEditorStart()
    {
        _state = GameModeState.Started;
        SceneFlow.SetCurrent(SceneManager.GetSceneByName("MainMenu"));
    }

    public IEnumerator OnEnd()
    {
        _state = GameModeState.Ending;
        yield return SceneManager.UnloadSceneAsync("MainMenu");
        _state = GameModeState.Ended;
    }

    public IEnumerator OnStart()
    {
        if (_state != GameModeState.Ended) yield break;
        _state = GameModeState.Starting;

        yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Additive);
        SceneFlow.SetCurrent(SceneManager.GetSceneByName("MainMenu"));
        _state = GameModeState.Started;
    }
}

public class StoryMode : IGameMode
{
    public bool IsPaused { get; set; }
    private GameModeState _state = GameModeState.Ended;
    public IEnumerator OnEnd()
    {
        if (_state == GameModeState.Ended)
            yield break;

        _state = GameModeState.Ending;

        Object.Destroy(ContextManager.Instance.player.gameObject);
        
        // Снимаем паузу (на всякий случай)
        IsPaused = false;

        // Получаем текущую игровую сцену
        var currentScene = SceneFlow.CurrentScene;

        // Выгружаем UI
        var uiScene = SceneManager.GetSceneByName("UI");
        if (uiScene.isLoaded)
        {
            yield return SceneManager.UnloadSceneAsync(uiScene);
        }

        // Выгружаем игровую сцену
        if (currentScene.IsValid() && currentScene.isLoaded)
        {
            yield return SceneManager.UnloadSceneAsync(currentScene);
        }

        // Сбрасываем SceneFlow
        SceneFlow.SetCurrent(default);

        _state = GameModeState.Ended;
    }

    public void Setup()
    {
        
    }

    public IEnumerator OnStart()
    {
        if (_state != GameModeState.Ended) yield break;
        _state = GameModeState.Starting;

        SaveManager.Instance.Load();

        var save = SaveManager.Instance;
        string sceneName = save.GetModule<SaveManifest>().Data.sceneName;

        if (string.IsNullOrEmpty(sceneName))
        {
            sceneName = "Level_01_Start";
        }

        yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        SceneFlow.SetCurrent(SceneManager.GetSceneByName(sceneName));

        yield return SceneManager.LoadSceneAsync("UI", LoadSceneMode.Additive);
        IsPaused = false;

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
        SaveManager.Instance.Load();
        _state = GameModeState.Started;
        SceneFlow.SetCurrent(SceneManager.GetActiveScene());
        Resume();
#endif
    }

    public void OnEditorEnd()
    {

    }
}


public enum GameModeState
{
    None,Starting,Started,Ending,Ended,
}