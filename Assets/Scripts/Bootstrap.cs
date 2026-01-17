using Sirenix.OdinInspector;
using Sirenix.Serialization;
using System.Collections.Generic;
using Systems;
using UnityEngine;

public class Bootstrap : SerializedMonoBehaviour
{
    public static Bootstrap instance;
    private static Bootstrap Instance { get { return instance; } set { instance = value; } }

    public ISaveModule[] modules;

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
    }

    [Button("SAVE")]
    public void Save()
    {
        SaveManager.Instance.Save();
    }


    [Button("LOAD")]
    public void Load()
    {
        SaveManager.Instance.Load();
    }
}