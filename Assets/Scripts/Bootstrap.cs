using Controllers;
using Sirenix.OdinInspector;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class Bootstrap : SerializedMonoBehaviour
{
    public static Bootstrap instance;
    private static Bootstrap Instance { get { return instance; } set { instance = value; } }

    public static PlayerController player;

    public ISaveModule[] modules;

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

        SaveManager.Instance.Modules = modules;

        Load();
    }

    [Button("SAVE")]
    public void Save()
    {
        SaveManager.Instance.Save();
    }

    [Button("RESET")]
    public void ResetData()
    {
        SaveManager.Instance.Reset();
    }


    [Button("LOAD")]
    public void Load()
    {
        SaveManager.Instance.Load();
    }
}