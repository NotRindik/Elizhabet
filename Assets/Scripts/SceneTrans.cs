using System;
using Sirenix.OdinInspector;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class SceneTrans : SerializedMonoBehaviour, IPassage
{
    public SceneHandle asset;

    [EnableIf("StartPointEnable")]
    [ValueDropdown("GetAvailableEntries")]
    public string Enter;
    public Transform spawnPos;
    public string EntryName => Enter;

    public Transform SpawnPos => spawnPos;

    public void Start()
    {
        SceneEntryRegistry.Instance.Register(this);
    }

    public void Transite()
    {
        for (int i = 0; i < asset.exits.Count; i++)
        {
            if(asset.exits[i].exit == Enter)
            {
                SceneLoader.Load(asset.exits[i].asset, asset.exits[i].Enter);
            }
        }
    }

    public bool StartPointEnable()
    {
        return asset != null;
    }

    private ValueDropdownList<string> GetAvailableEntries()
    {
        var list = new ValueDropdownList<string>();

        if (asset != null && asset.exits != null)
        {
            foreach (var e in asset.exits)
            {
                if (e != null)
                    list.Add(e.exit, e.exit);
            }
        }

        return list;
    }
}

public struct SceneTransitionSettings
{
    public string BlendInEffect;
    public string BlendOutEffect;
    public Action onTransitionFinished;
    public Action onBlendInFinished;
    public Action onBlendOutFinished;
    public float delay;
}


public static class SceneLoader
{
    public static string pendingEntry;

    public static void Load(SceneHandle handle, string entry, MonoBehaviour runner = null)
    {
        runner ??= App.Instance;
        runner.StartCoroutine(LoadProcess(handle.sceneAsset, entry));
    }
    
    public static void Load(string name,SceneTransitionSettings settings = default, MonoBehaviour runner = null)
    {
        runner ??= App.Instance;
        runner.StartCoroutine(LoadProcess(name,settings));
    }

    private static IEnumerator LoadProcess(string sceneName, string entry)
    {
        pendingEntry = entry;

        Scene oldScene = SceneFlow.CurrentScene;

        yield return TransitionEffect.Instance.BlendInCoroutine(0.3f);
        Object.DestroyImmediate(ContextManager.Instance.GlobalLight.gameObject);
        
        var loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        loadOp.allowSceneActivation = false;

        while (loadOp.progress < 0.9f)
            yield return null;

        
        loadOp.allowSceneActivation = true;

        while (!loadOp.isDone)
            yield return null;
        
        if (oldScene.IsValid())
        {
            var unloadOp = SceneManager.UnloadSceneAsync(oldScene);
            while (!unloadOp.isDone)
                yield return null;
        }
        yield return new WaitForSeconds(1);
        
        Scene newScene = SceneManager.GetSceneByName(sceneName);
        SceneFlow.SetCurrent(newScene);

        yield return TransitionEffect.Instance.BlendOutCoroutine(0.3f);
    }
    
    private static IEnumerator LoadProcess(string sceneName,SceneTransitionSettings settings = default)
    {
        if(settings.delay > 0)
            yield return new WaitForSeconds(settings.delay);
        
        Scene oldScene = SceneFlow.CurrentScene;

        yield return TransitionEffect.Instance.BlendInCoroutine(0.3f,settings.BlendInEffect,settings.onBlendInFinished);

        Object.DestroyImmediate(ContextManager.Instance.GlobalLight.gameObject);
        
        var loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        loadOp.allowSceneActivation = false;

        while (loadOp.progress < 0.9f)
            yield return null;
        
        loadOp.allowSceneActivation = true;

        while (!loadOp.isDone)
            yield return null;
        
        if (oldScene.IsValid())
        {
            AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(oldScene);
            if (unloadOp != null)
            {
                while (!unloadOp.isDone)
                    yield return null;
            }
        }
        
        Scene newScene = SceneManager.GetSceneByName(sceneName);
        
        
        SceneFlow.SetCurrent(newScene);
        
        yield return TransitionEffect.Instance.BlendOutCoroutine(0.3f,settings.BlendOutEffect,settings.onBlendOutFinished);
        
        settings.onTransitionFinished?.Invoke();
    }

    public static class SceneFlow
    {
        public static Scene CurrentScene { get; private set ; }

        public static void SetCurrent(Scene scene)
        {
            CurrentScene = scene;
        }
    }
}