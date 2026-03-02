using Sirenix.OdinInspector;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

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


public static class SceneLoader
{
    public static string pendingEntry;

    public static void Load(SceneHandle handle, string entry, MonoBehaviour runner = null)
    {
        runner ??= App.Instance;
        runner.StartCoroutine(LoadProcess(handle.sceneAsset.name, entry));
    }

    private static IEnumerator LoadProcess(string sceneName, string entry)
    {
        pendingEntry = entry;

        Scene currentActive = SceneManager.GetActiveScene();

        yield return TransitionEffect.Instance.BlendInCoroutine(0.3f);

        AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        loadOp.allowSceneActivation = true;


        while (!loadOp.isDone)
            yield return null;

        Scene newScene = SceneManager.GetSceneByName(sceneName);

        SceneManager.SetActiveScene(newScene);

        AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(currentActive);

        while (!unloadOp.isDone)
            yield return null;

        yield return TransitionEffect.Instance.BlendOutCoroutine(0.3f);
    }
}