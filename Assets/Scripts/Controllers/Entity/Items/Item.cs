using Assets.Scripts;
using Controllers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Systems;
using UnityEngine;

public class Item : OptimizedController, IInteractable
{
    public Action OnTake;
    public Action OnThrow;
    public Action<Item> OnRequestDestroy;

    protected ColorPositioningComponent colorPositioning;

    public List<Type> nonInitComponents = new List<Type>();
    public InputComponent inputComponent;
    public ItemPositioningSystem itemPositioningSystem;
    public Action itemPositioningHandler;

    public bool isSelected { get; set; }
    public bool EquipeOnStart;
    protected bool InitAfterInventory;
    protected Func<bool> DestroyCondition = () => true;
    protected Coroutine DestroyProcess;
    
    public ItemComponent itemComponent => GetControllerComponent<ItemComponent>();
    protected ControllersBaseFields baseFields => GetControllerComponent<ControllersBaseFields>();
    protected HealthComponent healthComponent => GetControllerComponent<HealthComponent>();

    protected override IComponent[] DefaultComponents => new IComponent[]{new ItemComponent(),new ControllersBaseFields(),new HealthComponent()};

    protected override ISystem[] DefaultSystems => new ISystem[]{new HealthSystem()};

    protected virtual void Start()
    {
        if (EquipeOnStart)
        {
            SelectItem(itemComponent.currentOwner);
        }
    }

    protected override void Awake()
    {
        base.Awake(); // Components/Systems уже собраны — GetControllerComponent тут уже валиден
        if (!InitAfterInventory)
        {
            healthComponent.currHealth = healthComponent.maxHealth;
            if (!PrefabCheacker.IsPrefab(itemComponent.itemPrefab))
            {
                string cleanedName = Regex.Replace(gameObject.name, @"\s*\([^)]*\)$", "");
                itemComponent.itemPrefab = Resources.Load<GameObject>($"{FileManager.Items}{cleanedName}");
            }
        }
    }

    // Важно: вызывать ПОСЛЕ того как Awake уже отработал (объект активен хотя бы один кадр) —
    // иначе base.Awake() своим BuildInfrastructure() затрёт то, что сюда запишешь.
    public virtual void InitAfterSpawnFromInventory(Dictionary<Type, IComponent> invComponents)
    {
        nonInitComponents.Add(typeof(ControllersBaseFields));

        foreach (var kvp in invComponents)
        {
            if (nonInitComponents.Contains(kvp.Key))
                continue;

            Components[kvp.Key] = kvp.Value; // ключ уже правильный runtime Type, обходим AddControllerComponent<T>
        }

        InitAfterInventory = true;
    }

    public virtual void SelectItem(AbstractEntity owner)
    {
        OnTake?.Invoke();
        isSelected = true;
        this.colorPositioning = owner.GetControllerComponent<ColorPositioningComponent>();
        itemComponent.currentOwner = owner;
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
        DestroyProcess ??= StartCoroutine(DestroyRoutine());
    }

    public IEnumerator DestroyRoutine()
    {
        while (true)
        {
            yield return new WaitUntil(DestroyCondition);
            OnBreak();
            Destroy(gameObject);
        }
    }

    protected virtual void OnBreak()
    {
        if (itemComponent.breakSound)
            AudioManager.instance.PlayEvent(new EventSoundInstance(itemComponent.breakSound));
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

    protected virtual void ReferenceClean()
    {
        if (isSelected)
            isSelected = false;
        else
            return;

        inputComponent = null;
        colorPositioning?.AfterColorCalculated.Remove(itemPositioningHandler);
        itemPositioningHandler = null;
        itemPositioningSystem = null;
        itemComponent.currentOwner = null;
        this.colorPositioning = null;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        ReferenceClean();
        OnRequestDestroy?.Invoke(this);
        OnRequestDestroy = null;
    }

    void IInteractable.Interact(AbstractEntity interactor)
    {
        var inventory = interactor.GetControllerSystem<InventorySystem>();
        inventory?.SetItem(this);
    }

    public bool CanInteract(AbstractEntity _) => !isSelected && isActiveAndEnabled;
}

[Serializable]
public class ItemComponent : IComponent
{
    public GameObject itemPrefab;
    public AbstractEntity currentOwner;
    public Sprite itemIcon;
    public int stackSize;
    public EventSound breakSound;
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
        if (owner is Item item)
            _itemOwner = item;
        else
        {
            UnityEngine.Debug.LogError("Ты суешь не предмет в позиционирование предметов");
            return;
        }
        _itemComponent = _itemOwner.GetControllerComponent<ItemComponent>();
        _colorPositioning = _itemComponent.currentOwner.GetControllerComponent<ColorPositioningComponent>();
    }
    public virtual void ItemPositioning() { }
}

public class OneHandPositioning : ItemPositioningSystem
{
    public override void ItemPositioning()
    {
        if (_colorPositioning == null)
            return;

        _itemOwner.transform.position = _colorPositioning.pointsGroup[ColorPosNameConst.RIGHT_HAND_POS].FirstActivePoint();
        _itemOwner.transform.position += new Vector3(0, 0, -1);
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
        _itemOwner.transform.position += new Vector3(0, 0, -1);
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