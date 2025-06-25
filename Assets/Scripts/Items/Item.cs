using Assets.Scripts;
using Controllers;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Systems;
using UnityEngine;

public abstract class Item : EntityController
{
    public Action OnTake;
    public Action OnThrow;

    Controller owner;
    ColorPositioningComponent colorPositioning;

    public List<Type> nonInitComponents = new List<Type>();
    public ItemComponent itemComponent;
    public InputComponent inputComponent;

    protected bool InitAfterInventory;
    
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
            healthComponent.currHealth = healthComponent.maxHealth;
        }
    }
    public virtual void InitAfterSpawnFromInventory(Dictionary<Type, IComponent> invComponents)
    {
        EntitySetup();
        nonInitComponents.Add(typeof(ControllersBaseFields));
        foreach (var field in FieldInfos)
        {
            if (typeof(IComponent).IsAssignableFrom(field.FieldType))
            {
                if (invComponents.TryGetValue(field.FieldType, out var value))
                {
                    if (!nonInitComponents.Contains(field.FieldType))
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
        inputComponent = new InputComponent(owner.GetControllerSystem<IInputProvider>());
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
        foreach (var col in baseFields.collider)
        {
            col.enabled = true;   
        }
        ReferenceClean();
    }
    protected virtual void ReferenceClean()
    {
        inputComponent = null;
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
        ReferenceClean();
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
 }
 public class InputComponent : IComponent
 {
     public InputComponent(IInputProvider input)
     {
         this.input = input;
     }
     
     public IInputProvider input;
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
