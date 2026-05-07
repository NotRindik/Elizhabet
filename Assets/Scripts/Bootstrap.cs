using Assets.Scripts;
using Controllers;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

public interface IGameService
{
    void Init();
}

public static class CoreBootstrapper
{
    public const string CoreScene = "Core";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Main()
    {
        var active = SceneManager.GetActiveScene();
        var inst = Object.Instantiate(Resources.Load<App>($"{FileManager.Prefabs}__ App"));

        var coreScene = SceneManager.GetSceneByName(CoreScene);

        if (coreScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(inst.gameObject, coreScene);
            return;
        }

        if (active.name == CoreScene)
        {
            SceneManager.MoveGameObjectToScene(inst.gameObject, active);
            return;
        }

        Debug.Log("BOOTSTRAP");
        SceneManager.LoadScene(CoreScene, LoadSceneMode.Additive);
        
        coreScene = SceneManager.GetSceneByName(CoreScene);
        SceneManager.MoveGameObjectToScene(inst.gameObject, coreScene);
    }
}

[DefaultExecutionOrder(-1000)]
public class Bootstrap : SerializedMonoBehaviour
{
    public static Bootstrap instance;
    private static Bootstrap Instance { get { return instance; } set { instance = value; } }

    public ItemsDataBase itemDB;

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(Instance.gameObject);
            Instance = this;
        }
    }
}