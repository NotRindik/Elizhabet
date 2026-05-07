using System;
using UnityEngine;
using UnityEngine.Serialization;

public class StoryModeServices : MonoBehaviour,IGameService
{
    public GameObject[] StoryModePrefabs;
    public IStoryModeService[] _temp;
    public GameModeManager gameModeManager;

    public bool isInited;

    public void OnGameModeChange(IGameMode gameMode)
    {
        Debug.Log("WWWWWW " + gameMode);
        if (gameMode is not StoryMode)
        {
            if (isInited)
            {
                foreach (var service in _temp)
                {
                    Destroy(service.Mono.gameObject);
                    service.Cleanup();
                }
            }
            
            isInited = false;
            return;
        }

        isInited = true;
        Debug.Log("YEAH " + gameMode);
        _temp = new IStoryModeService[StoryModePrefabs.Length];

        for (int i = 0; i < _temp.Length; i++)
        {
            var go = Instantiate(StoryModePrefabs[i], transform);
            _temp[i] = go.GetComponent<IStoryModeService>();
            _temp[i].Init();
        }
    }
    public void Init()
    {
        gameModeManager.OnGameModeChange += OnGameModeChange;
        OnGameModeChange(gameModeManager.CurrMode);
    }

    private void OnDestroy()
    {
        isInited = false;
        gameModeManager.OnGameModeChange -= OnGameModeChange;
    }
}

public interface IStoryModeService
{
    public MonoBehaviour Mono { get; }
    public void Init();
    public void Cleanup();
}
