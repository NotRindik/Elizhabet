using Sirenix.OdinInspector;
using UnityEngine;

public class SaveManagerCore : SerializedMonoBehaviour, IGameService
{
    public static SaveManagerCore Instance;

    public void Init()
    {
        if (Instance == null)
            Instance = this;
        Debug.Log("Save Core Inited");
        SaveManager.Instance.Modules = SaveManagerCore.Instance.modules;
    }
    public ISaveModule[] modules;
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

    private void OnDestroy()
    {
        Instance = null;
    }
}
