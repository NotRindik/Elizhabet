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
        if (SceneManager.GetSceneByName(CoreScene).isLoaded)
            return;

        SceneManager.LoadScene(CoreScene, LoadSceneMode.Additive);
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