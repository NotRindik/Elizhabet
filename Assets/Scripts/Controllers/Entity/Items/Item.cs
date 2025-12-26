using Assets.Scripts;
using Controllers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Systems;
using UnityEngine;

public abstract class Item : EntityController,ITakeAbleSystem
{
    public Action OnTake;
    public Action OnThrow;
    
    protected ColorPositioningComponent colorPositioning;

    public List<Type> nonInitComponents = new List<Type>();
    public ItemComponent itemComponent;
    public InputComponent inputComponent;
    public ItemPositioningSystem itemPositioningSystem;
    public string itemPositioning;

    public Action itemPositioningHandler;

    public bool isSelected {  get; set; }

    public bool EquipeOnStart;

    protected bool InitAfterInventory;
    
    protected virtual void Start()
    {
        if (!InitAfterInventory)
        {
            EntitySetup();
            healthComponent.currHealth = healthComponent.maxHealth;
            if (!PrefabCheacker.IsPrefab(itemComponent.itemPrefab))
            {
                string cleanedName = Regex.Replace(gameObject.name, @"\s*\(\d+\)$", "");
                itemComponent.itemPrefab = Resources.Load<GameObject>($"{FileManager.Items}{cleanedName}");
            }
        }

        if (EquipeOnStart)
        {
            SelectItem(itemComponent.currentOwner);
        }
    }
    protected override void Awake() {}
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
    public virtual void SelectItem(AbstractEntity owner)
    {
        OnTake?.Invoke();
        isSelected = true;
        this.colorPositioning = owner.GetControllerComponent<ColorPositioningComponent>(); 
        itemComponent.currentOwner = (EntityController)owner;
        inputComponent = new InputComponent(owner.GetControllerSystem<IInputProvider>());
        baseFields.rb.bodyType = RigidbodyType2D.Static;

        if (colorPositioning != null)
        {
            itemPositioningSystem = new OneHandPositioning();
            itemPositioningSystem.Initialize(this);
            AddControllerSystem(itemPositioningSystem);
            itemPositioningHandler = () => itemPositioningSystem?.ItemPositioning();
            colorPositioning.AfterColorCalculated.Add(itemPositioningHandler, 3);
        }
        else
        {
            itemPositioningSystem = new ZeroPositioning();
            itemPositioningSystem.Initialize(this);
            AddControllerSystem(itemPositioningSystem);
            itemPositioningHandler = () => itemPositioningSystem?.ItemPositioning();
            OnLateUpdate += itemPositioningHandler;
        }


        foreach (var col in baseFields.collider)
        {
            col.isTrigger = true;   
        }
    }

    public virtual void DestroyItem()
    {
        Destroy(gameObject);
    }

    public virtual void Throw(Vector2 dir = default, float force = 15) 
    {
        OnThrow?.Invoke();
        baseFields.rb.bodyType = RigidbodyType2D.Dynamic;
        if (dir == default)
            dir = (itemComponent.currentOwner.mono.transform.position - transform.position);

        baseFields.rb.AddForce(dir * force, ForceMode2D.Impulse);
        foreach (var col in baseFields.collider)
        {
            col.isTrigger = false;   
        }
        ReferenceClean();
    }
    protected override void ReferenceClean()
    {
        if(isSelected)
            isSelected = false;
        else
        {
            return;
        }
        inputComponent = null;
        colorPositioning?.AfterColorCalculated.Remove(itemPositioningHandler);
        itemPositioningHandler = null;
        itemPositioningSystem = null;
        this.colorPositioning = null;
    }

    public override void LateUpdate()
    {
        base.LateUpdate();
        itemPositioning = itemPositioningSystem?.GetType().ToString();
    }
    public override void OnDestroy()
    {
        base.OnDestroy();
        Debug.Log($"Destroyed {gameObject}");
        OnRequestDestroy?.Invoke(this);
        OnRequestDestroy = null;
    }
}
[Serializable]
 public class ItemComponent : IComponent
 {
     public GameObject itemPrefab;
     public AbstractEntity currentOwner;
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
public static class PrefabCheacker
{
    public static bool IsPrefab(GameObject obj)
    {
        return obj?.scene.rootCount == 0;
    }
}

public abstract class ItemPositioningSystem : BaseSystem
{
    protected ColorPositioningComponent _colorPositioning;
    protected ItemComponent _itemComponent;
    protected Item _itemOwner;

    public override void Initialize(AbstractEntity owner)
    {
        
        base.Initialize(owner);
        if(owner is Item item)
            _itemOwner = item;
        else
        {
            UnityEngine.Debug.LogError("Ты суешь не предмет в позиционирование предметов");
            return;
        }
        _itemComponent = _itemOwner.GetControllerComponent<ItemComponent>();
        _colorPositioning = _itemComponent.currentOwner.GetControllerComponent<ColorPositioningComponent>();
    }
    public virtual void ItemPositioning(){}
}

public class OneHandPositioning : ItemPositioningSystem
{
    public override void ItemPositioning()
    {
        if (_colorPositioning == null)
            return;

        _itemOwner.transform.position = _colorPositioning.pointsGroup[ColorPosNameConst.RIGHT_HAND_POS].FirstActivePoint();
        
        Vector2 collinearDirection = -_colorPositioning.pointsGroup[ColorPosNameConst.RIGHT_HAND_POS].direction.normalized;
        float angle = Mathf.Atan2(collinearDirection.y, collinearDirection.x) * Mathf.Rad2Deg;
        _itemOwner.transform.rotation = Quaternion.Euler(0, 0, angle);
        _itemOwner.transform.localScale = new Vector3(1, _itemComponent.currentOwner.mono.transform.localScale.x, 1);
    }
}


public class ZeroPositioning : ItemPositioningSystem
{
    public override void ItemPositioning()
    {
        _itemOwner.transform.localPosition = Vector2.zero;
    }
}


public class TwoHandPositioning : ItemPositioningSystem
{
    public override void ItemPositioning()
    {
        if (_colorPositioning == null)
            return;
        Vector3 leftHand = _colorPositioning.pointsGroup[ColorPosNameConst.LEFT_HAND].FirstActivePoint();
        Vector3 rightHand = _colorPositioning.pointsGroup[ColorPosNameConst.RIGHT_HAND_POS].FirstActivePoint();
        Vector2 collinearDirection;
        float angle;
        if (leftHand == Vector3.zero)
        {
            _itemOwner.transform.position = _colorPositioning.pointsGroup[ColorPosNameConst.RIGHT_HAND_POS].FirstActivePoint();
        
            collinearDirection = -_colorPositioning.pointsGroup[ColorPosNameConst.RIGHT_HAND_POS].direction.normalized;
            angle = Mathf.Atan2(collinearDirection.y, collinearDirection.x) * Mathf.Rad2Deg;
            _itemOwner.transform.rotation = Quaternion.Euler(0, 0, angle);
            _itemOwner.transform.localScale = new Vector3(1, _itemComponent.currentOwner.mono.transform.localScale.x, 1);
            return;
        }
        _itemOwner.transform.position = rightHand;
        
        collinearDirection = (rightHand - leftHand) * _itemComponent.currentOwner.mono.transform.localScale.x;
        angle = Mathf.Atan2(collinearDirection.y, collinearDirection.x) * Mathf.Rad2Deg;
        _itemOwner.transform.rotation = Quaternion.Euler(0, 0, angle + 90f);
        _itemOwner.transform.localScale = new Vector3(1, _itemComponent.currentOwner.mono.transform.localScale.x, 1);
    }
}

[Serializable]
public class RarityComponent : IComponent
{
    public Rarity rarity;
    public enum Rarity
    {
        Common, Uncommon, Rare, Elite, Epic, Legendary, Cult, Cum, Cursed
    }

    public static readonly Dictionary<Rarity, string> RarityNames = new()
    {
        { Rarity.Common, "Common" },
        { Rarity.Uncommon, "Uncommon" },
        { Rarity.Rare, "Rare" },
        { Rarity.Elite, "Elite" },
        { Rarity.Epic, "Epic" },
        { Rarity.Legendary, "Legendary" },
        { Rarity.Cum, "Cuming" },
        { Rarity.Cult, "Cult Weapon" },
        { Rarity.Cursed, "Cursed Weapon" }
    };
}
