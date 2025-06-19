using Assets.Scripts;
using Controllers;
using System;
using System.Text.RegularExpressions;
using Systems;
using UnityEngine;
using AYellowpaper.SerializedCollections;

public abstract class Item : EntityController
{
    public Action OnTake;
    public Action OnThrow;

    Controller owner;
    ColorPositioningComponent colorPositioning;

    public ItemComponent itemComponent;

    protected bool InitAfterInventory;
    

    protected virtual void Start()
    {
        if (!PrefabCheacker.IsPrefab(itemComponent.itemPrefab) && InitAfterInventory == false)
        {
            string cleanedName = Regex.Replace(gameObject.name, @"\s*\(\d+\)$", "");
            itemComponent.itemPrefab = Resources.Load<GameObject>($"{FileManager.Items}{cleanedName}");
        }
    }

    public void InitAfterSpawnFromInventory(SerializedDictionary<Type,IComponent> component)
    {
        components = component;
        InitAfterInventory = true;
    }
    public virtual void TakeUp(ColorPositioningComponent colorPositioning, Controller owner)
    {
        OnTake?.Invoke();
        this.colorPositioning = colorPositioning; 
        this.owner = owner;
        baseFields.rb.bodyType = RigidbodyType2D.Static;
        foreach (var col in baseFields.collider)
        {
            col.enabled = false;   
        }
    }

    public virtual void DestroyItem()
    {
        var inventoryComponent = itemComponent.currentOwner.GetControllerComponent<InventoryComponent>();
        var inventorySystem = itemComponent.currentOwner.GetControllerSystem<InventorySystem>();
        int activeIndex = inventoryComponent.CurrentActiveIndex;
        var stack = inventoryComponent.items[activeIndex];
        
        stack.RemoveItem(itemComponent);
        
        if (!inventoryComponent.items.Contains(stack))
        {
            int clampedIndex = Mathf.Clamp(
                inventoryComponent.CurrentActiveIndex, 
                0, 
                Mathf.Max(inventoryComponent.items.Count - 1, 0)
            );
            
            int newActiveIndex;
            if (clampedIndex < inventoryComponent.items.Count - 1)
            {
                newActiveIndex = clampedIndex + 1;
            }
            else if (clampedIndex > 0)
            {
                newActiveIndex = clampedIndex - 1;
            }
            else
            {
                newActiveIndex = 0;
            }
            
            if (newActiveIndex >= 0 && newActiveIndex < inventoryComponent.items.Count)
            {
                inventorySystem.SetActiveWeaponWithoutDestroy(newActiveIndex);
            }
            else
            {
                inventorySystem.SetActiveWeaponWithoutDestroy(-1);
            }
        }
        else
        {
            inventorySystem.SetActiveWeaponWithoutDestroy(activeIndex);
        }

        Destroy(gameObject);
    }

    public virtual void Throw()
    {
        OnThrow?.Invoke();
        baseFields.rb.bodyType = RigidbodyType2D.Dynamic;
        baseFields.rb.AddForce((owner.transform.position - transform.position) * 15,ForceMode2D.Impulse);
        foreach (var col in baseFields.collider)
        {
            col.enabled = true;   
        }
        this.colorPositioning = null;
        this.owner = null;
    }

    public virtual void LateUpdate()
    {
        if (colorPositioning == null)
            return;

        transform.position = colorPositioning.pointsGroup[ColorPosNameConst.RIGHT_HAND_POS].FirstActivePoint();

        Vector2 perpendicularDirection = new Vector2(-colorPositioning.pointsGroup[ColorPosNameConst.RIGHT_HAND_POS].direction.y, colorPositioning.pointsGroup[ColorPosNameConst.RIGHT_HAND_POS].direction.x);
        Vector2 collinearDirection = -colorPositioning.pointsGroup[ColorPosNameConst.RIGHT_HAND_POS].direction.normalized;
        float angle = Mathf.Atan2(collinearDirection.y, collinearDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
        transform.localScale = new Vector3(1, owner.transform.localScale.x, 1);
    }
}

[Serializable]
public class ItemComponent : IComponent
{
    public TakeType takeType;
    public GameObject itemPrefab;
    public EntityController currentOwner;
    public Sprite itemIcon;
}
[Serializable]
public class DurabilityComponent: IComponent
{
    public int maxDurability = 1;
    private int _durabilty;
    public Action<int> OnDurabilityChange;
    public int Durability
    {
        get
        {
            return _durabilty;
        }
        set
        {
            _durabilty = value;
            OnDurabilityChange?.Invoke(value);
        }
    }
}

public enum TakeType
{
    None,
    ParallelToHand,
    PerpendicularToHand
}

public static class PrefabCheacker
{
    public static bool IsPrefab(GameObject obj)
    {
        return obj.scene.rootCount == 0;
    }
}