using System;
using Controllers;
using Init;
using Systems;
using UnityEngine;
using UnityEngine.EventSystems;
public abstract class SlotBase : MonoBehaviour,IInitializable<(int,AbstractEntity)>,IDropHandler
{
    protected DragableItem _itemVisual;
    protected DragableItem ItemVisual
    {
        get => _itemVisual;
        set
        {
            if (_itemVisual != null)
                _itemVisual.OnClick -= OnItemClick;

            if (value != null)
                value.OnClick += OnItemClick;

            _itemVisual = value;
        }
    }
    protected Controller Owner;
    protected InventorySystem InventorySystem;
    protected InventoryComponent InventoryComponent;
    protected InventorySlotsComponent InventorySlotsComponent;
    public int Index { get; protected set; }
    
    public abstract bool CanAccept(DragableItem item);
    
    public virtual void SetData(InventoryItemData item)
    {
        ItemVisual = DrawItem(item);
        ItemVisual?.SetSourceSlot(BuildSlotRef());
    }
    public virtual bool TrySetItem(DragableItem item)
    {
        if (CanAccept(item))
        {
            ItemVisual = item;
            ItemVisual.parentAfterDrag = transform;
            ItemVisual.transform.SetAsLastSibling();
            
            item.slotIndex = Index;
            UpdateItemData(item);
            return true;
        }
        return false;
    }
    public DragableItem GetItem() => ItemVisual;
    public virtual void DestroyVisual()
    {
        if (ItemVisual)
        {
            Destroy(ItemVisual.gameObject);
        }
        ItemVisual = null;
    }
    public virtual void Clear()
    {
        ItemVisual = null;
    }
    

    public bool IsEmpty => GetItem() == null;

    protected DragableItem DrawItem(InventoryItemData item)
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        if (item == null || item.Item == null || item?.Item?.Count == 0)
            return null;

        var instance = Instantiate(
            Owner.GetControllerComponent<InventorySlotsComponent>().itemPrefab,
            transform.position,
            Quaternion.identity
        );
        instance.slotIndex = Index;

        instance.itemData = item;
        UpdateItemData(instance);

        var itemComponent = item.Item.GetItemComponent<ItemComponent>();
        instance.image.sprite = itemComponent?.itemIcon;
        instance.image.color = Color.white;
        instance.transform.SetParent(transform, false);
        instance.parentAfterDrag = transform;
        instance.transform.position = transform.position;
        return instance;
    }

    private void UpdateItemData(DragableItem instance)
    {
        instance.itemData.SlotIndex = Index;
    }

    public virtual void Init((int ,AbstractEntity) param)
    {
        Index = param.Item1;
        Owner = (Controller)param.Item2;
        OnInitialized();
    }
    public virtual void OnInitialized()
    {
        InventorySystem = Owner.GetControllerSystem<InventorySystem>();
        InventoryComponent = Owner.GetControllerComponent<InventoryComponent>();
        InventorySlotsComponent = Owner.GetControllerComponent<InventorySlotsComponent>();
    }

    public virtual void OnItemClick()
    {
        return;
    }
    
    public virtual void OnDrop(PointerEventData eventData)
    {
        var dragItem = eventData.pointerDrag?.GetComponent<DragableItem>();
        if (dragItem == null) return;
        if (!CanAccept(dragItem)) return;

        InventorySystem.MoveOrSwap(dragItem.SourceSlot, BuildSlotRef());
        // RedrawHotBar / RedrawStorage сработают через подписку
    }
    
    public abstract InventorySystem.SlotRef BuildSlotRef();
}

namespace Init
{
    public interface IInitializable<in T>
    {
        void Init(T param);
    }   
}