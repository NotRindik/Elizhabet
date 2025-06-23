using Assets.Scripts;
using Controllers;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Systems;
using UnityEngine;

public abstract class Item : EntityController
{
    public Action OnTake;
    public Action OnThrow;

    Controller owner;
    ColorPositioningComponent colorPositioning;

    public ItemComponent itemComponent;

    protected bool InitAfterInventory;

    public Action<Item> OnRequestDestroy;
    public DurabilityComponent durabilityComponent;

    protected virtual void Start()
    {
        if (!PrefabCheacker.IsPrefab(itemComponent.itemPrefab) && InitAfterInventory == false)
        {
            string cleanedName = Regex.Replace(gameObject.name, @"\s*\(\d+\)$", "");
            itemComponent.itemPrefab = Resources.Load<GameObject>($"{FileManager.Items}{cleanedName}");
        }
    }
    protected override void Awake()
    {
        if (!InitAfterInventory)
        {
            EntitySetup();
            durabilityComponent.Durability = durabilityComponent.maxDurability;
        }
    }
    public virtual void InitAfterSpawnFromInventory(Dictionary<Type, IComponent> invComponents)
    {
        EntitySetup();

        foreach (var field in FieldInfos)
        {
            if (typeof(IComponent).IsAssignableFrom(field.FieldType))
            {
                if (invComponents.TryGetValue(field.FieldType, out var value))
                {
                    if (!(value is ControllersBaseFields))
                    {
                        field.SetValue(this, value);
                        
                        Components[field.FieldType] = value;
                    }
                }
            }
        }

        InitAfterInventory = true;
    }
    public virtual void SelectItem(Controller owner)
    {
        OnTake?.Invoke();
        this.colorPositioning = owner.GetControllerComponent<ColorPositioningComponent>(); 
        this.owner = owner;
        itemComponent.input = owner.GetControllerSystem<IInputProvider>();
        baseFields.rb.bodyType = RigidbodyType2D.Static;
        foreach (var col in baseFields.collider)
        {
            col.enabled = false;   
        }
    }

    public virtual void DestroyItem()
    {
        Destroy(gameObject);
    }

    public virtual void Throw() 
    {
        OnThrow?.Invoke();
        baseFields.rb.bodyType = RigidbodyType2D.Dynamic;
        baseFields.rb.AddForce((owner.transform.position - transform.position) * 15,ForceMode2D.Impulse);
        itemComponent.input = null;
        foreach (var col in baseFields.collider)
        {
            col.enabled = true;   
        }
        this.colorPositioning = null;
        this.owner = null;
    }

    public virtual void LateUpdate()
    {
        WeaponPositioning();
    }
    protected virtual void WeaponPositioning()
    {

        if (colorPositioning == null)
            return;

        transform.position = colorPositioning.pointsGroup[ColorPosNameConst.RIGHT_HAND_POS].FirstActivePoint();
        
        Vector2 collinearDirection = -colorPositioning.pointsGroup[ColorPosNameConst.RIGHT_HAND_POS].direction.normalized;
        float angle = Mathf.Atan2(collinearDirection.y, collinearDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
        transform.localScale = new Vector3(1, owner.transform.localScale.x, 1);
    }
    public override void OnDestroy()
    {
        base.OnDestroy();
        OnRequestDestroy?.Invoke(this);
        OnRequestDestroy = null; 
    }
}
[Serializable]
 public class ItemComponent : IComponent
 {
     public GameObject itemPrefab;
     public EntityController currentOwner;
     public Sprite itemIcon;
     public IInputProvider input;
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
        return obj?.scene.rootCount == 0;
    }
}
