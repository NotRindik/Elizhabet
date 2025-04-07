using Assets.Scripts;
using Controllers;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Systems;
using UnityEngine;

public abstract class Items : MonoBehaviour
{
    public Action OnTake;
    public Action OnThrow;

    public Rigidbody2D rb;
    public Collider2D col;

    Controller owner;
    ColorPositioningComponent colorPositioning;

    public ItemComponent itemComponent;

    private bool initAfterInventory;

    protected virtual void Start()
    {
        if (!PrefabCheacker.IsPrefab(itemComponent.itemPrefab) && initAfterInventory == false)
        {
            string cleanedName = Regex.Replace(gameObject.name, @"\s*\(\d+\)$", "");
            itemComponent.itemPrefab = Resources.Load<GameObject>($"{FileManager.Items}{cleanedName}");
            itemComponent.durability = itemComponent.maxDurability;
        }
    }

    public void InitAfterSpawnFromInventory(ItemComponent itemComponent)
    {
        this.itemComponent = itemComponent;
        initAfterInventory = true;
    }
    public virtual void TakeUp(ColorPositioningComponent colorPositioning, Controller owner)
    {
        OnTake?.Invoke();
        this.colorPositioning = colorPositioning; 
        this.owner = owner;
        rb.bodyType = RigidbodyType2D.Static;
        col.enabled = false;
    }

    public virtual void DestroyItem()
    {
        var inventoryComponent = itemComponent.currentOwner.GetControllerComponent<InventoryComponent>();
        var inventorySystem = itemComponent.currentOwner.GetControllerSystem<InventorySystem>();
        int activeIndex = inventoryComponent.CurrentActiveIndex;
        
        inventoryComponent.items[activeIndex].RemoveItem(itemComponent);
        if (inventoryComponent.items[activeIndex].Count == 0)
        {
            inventoryComponent.items.RemoveAt(activeIndex);
            if (inventoryComponent.CurrentActiveIndex < inventoryComponent.items.Count - 1)
            {
                inventorySystem.SetActiveWeaponWithoutDestroy(activeIndex + 1);
            }
            else
            {
                inventorySystem.SetActiveWeaponWithoutDestroy(activeIndex-1);
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
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.AddForce((owner.transform.position - transform.position) * 15,ForceMode2D.Impulse);
        col.enabled = true;
        this.colorPositioning = null;
        this.owner = null;
    }

    public virtual void LateUpdate()
    {
        if (colorPositioning == null)
            return;

        foreach (var point in colorPositioning.points)
        {
            var pos = point.position;
            if (pos != Vector3.zero)
            {
                transform.position = pos;
                break;
            }
        }

        
        Vector2 perpendicularDirection = new Vector2(-colorPositioning.direction.y, colorPositioning.direction.x);
        Vector2 collinearDirection = -colorPositioning.direction.normalized;
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
    public int maxDurability;
    private int _durabilty;
    public Action<int> OnDurabilityChange;
    public int durability
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