using System;
using Systems;
using UnityEngine;

public class PlayerInventory : MonoBehaviour, IStoryModeService
{
    public InventoryComponent InventoryComponent;

    public static PlayerInventory Instance;

    public MonoBehaviour Mono { get => this; }
    
    public void Init()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(Instance);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public void Cleanup()
    {
        Instance = null;
    }
}
