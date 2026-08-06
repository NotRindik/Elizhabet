using System;
using System.Collections.Generic;
using Controllers;
using NUnit.Framework;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class ContextManager : MonoBehaviour
{
    public static ContextManager Instance;

    private PlayerController _player;

    public PlayerController player
    {
        get
        {
            return  _player;
        }
        set
        {
            _player = value;
            EventBus.OnPlayerChange?.Invoke(_player);
        }
    }

    public Dictionary<string, SaveCapsule> SaveCapsulesInLevel { get; private set; } = new Dictionary<string, SaveCapsule>();

    public ExtraSpawnManager extraSpawnManager = new();

    public Action<Camera> OnCameraChange;

    public Camera temp;
    public Camera mainCamera { get 
        {
            if (temp == null)
            {
                temp = Camera.main;
                OnCameraChange?.Invoke(temp);
            }
            return temp;
        } }

    public void RegisterCapsule(SaveCapsule saveCapsule) => SaveCapsulesInLevel.Add(saveCapsule.ID, saveCapsule);

    private void Awake()
    {
        if(Instance == null) 
            Instance = this;
        else
        {
            Destroy(Instance);
            Instance = this;
        }
    }

    private void OnDestroy()
    {
        extraSpawnManager.Dispose();
    }
}
