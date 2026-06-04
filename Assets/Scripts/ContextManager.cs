using System;
using Controllers;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class ContextManager : MonoBehaviour
{
    public static ContextManager Instance;

    public PlayerController player;

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
