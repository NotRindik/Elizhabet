using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public class App : SerializedMonoBehaviour
{
    public List<IGameService> GameServices;

    public static bool IsEditor { get; set; }

    public static App Instance;
    public void Awake()
    {
        if(Instance == null)
            Instance = this;
        foreach (var service in GameServices)
            service.Init();
    }
    private void Start()
    {
        SceneManager.SetActiveScene(SceneManager.GetSceneByName(CoreBootstrapper.CoreScene));
    }

    public T GetService<T>()
    {
        foreach(var service in GameServices)
            if(service is T)
                return (T)service;
        return default;
    }

    private void OnDestroy()
    {
        Instance = null;
        SceneLoader.pendingEntry = "";
    }
}
