using System;
using System.Collections.Generic;
using System.Linq;
using Controllers;
using std;
using Systems;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerSaveLoadManager : MonoBehaviour
{
    public PlayerController playerController;

    public PlayerSave PlayerSave;

    public bool isInited;
    
    private Action _isPlayerLoadReady;

    public event Action IsPlayerLoadReady
    {
        add
        {
            _isPlayerLoadReady += value;
            
            if(isInited)
                value?.Invoke();

        }
        remove => _isPlayerLoadReady -= value;
    }

    private void Awake()
    {
        PlayerSave = SaveManager.Instance.GetModule<PlayerSave>();
    }

    private void Start()
    {
        SetToNullSlots();
        if(SaveManager.Instance.GetModule<SaveManifest>().Data.sceneName == SceneLoader.SceneFlow.CurrentScene.name)
            PlacePlayerToCorrectPos();
        
        
        _isPlayerLoadReady?.Invoke();
        isInited = true;
    }

    public void PrepareData(SaveCapsule saveCapsule)
    {
        PlayerSave.Data.lastSaveScene = SceneLoader.SceneFlow.CurrentScene.name;
        PlayerSave.Data.openedAbility = LinqUtility.ToHashSet(playerController.abilitieContainer.Raw);
        PlayerSave.Data.lastSaveCapsuleId = saveCapsule.ID;
        
        var saveInventory = PlayerSave.Data.inventory;

        var inv = playerController.GetControllerComponent<InventoryComponent>();
        
        saveInventory.hotBar = inv.hotBar.Raw.Select(InventorySaveUtility.Capture).ToList();
        saveInventory.storage = inv.storage.Raw.Select(InventorySaveUtility.Capture).ToList();
        saveInventory.armor = inv.armor.Raw.Select(InventorySaveUtility.Capture).ToList();
        saveInventory.accessories = inv.accessories.Raw.Select(InventorySaveUtility.Capture).ToList();
    }

    public void SetToNullSlots()
    {
        var data = PlayerSave.Data;
        var inv = playerController.GetControllerComponent<InventoryComponent>();
        var saveInventory = data.inventory;

        InsertIntoNullSlots(inv.hotBar.Raw, saveInventory.hotBar, inv);
        InsertIntoNullSlots(inv.armor.Raw, saveInventory.armor, inv);
        InsertIntoNullSlots(inv.accessories.Raw, saveInventory.accessories, inv);

        foreach (var stack in saveInventory.storage)
        {
            inv.storage.TryAdd(InventorySaveUtility.Restore(stack, inv));
        }
    }

    private static void InsertIntoNullSlots(List<ItemStack> target, List<ItemStackSave> source, InventoryComponent inventory)
    {
        int sourceIndex = 0;

        for (int i = 0; i < target.Count && sourceIndex < source.Count; i++)
        {
            if (target[i] != null)
                continue;

            target[i] = InventorySaveUtility.Restore(source[sourceIndex], inventory);
            sourceIndex++;
        }
    }

    public void PlacePlayerToCorrectPos()
    {
        var data = PlayerSave.Data;
        if(string.IsNullOrEmpty(data.lastSaveCapsuleId))
            return;
        
        if(ContextManager.Instance.SaveCapsulesInLevel.TryGetValue(data.lastSaveCapsuleId,out var value))
            value.SpawnInsideNoSave(playerController);
    }
}

public static class InventorySaveUtility
{
    public static ItemStackSave Capture(ItemStack itemStack)
    {
        if (itemStack == null)
            return null;
        
        var save = new ItemStackSave { itemName = itemStack.itemName };

        foreach (var items in itemStack.items)
        {
            save.instances.Add(
                new ItemInstanceSave
                {
                    SerializedComponent = items.Where(pair => pair.Value is ISaveSerialize)
                        .ToDictionary(
                            pair => pair.Key,
                            pair => (ISaveSerialize)pair.Value
                        )
                }
            );

        }
        return save;
    }
    
    public static ItemStack Restore(ItemStackSave save, InventoryComponent invComponent)
    {
        if (save == null)
            return null;

        var prefabItem = GameResourcesManager.Instance.ItemsDataBase.Get(save.itemName);
        
        var stack = new ItemStack(save.itemName, invComponent, prefabItem.GetControllerComponentDirect<ItemComponent>().stackSize);
        
        foreach (var items in save.instances)
        {
            stack.AddItem(items.SerializedComponent);
        }
        
        return stack;
    }
}


