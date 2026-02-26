using Assets.Scripts;
using Controllers;
using Sirenix.OdinInspector;
using UnityEngine;

public interface IGameService
{
    void Init();
}

public static class CoreBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Main()
    {
        var app = Object.Instantiate(Resources.Load($"{FileManager.Prefabs}__ App")) as GameObject;
        if (app == null)
            throw new System.ApplicationException();

        Object.DontDestroyOnLoad(app.gameObject);
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