using System;
using Controllers;
using Init;
using Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using static UnityEditor.Progress;

public abstract class SlotBase : MonoBehaviour,IInitializable<(int,Controller)>,IDropHandler
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
    public int currPage;
    public int Index { get; protected set; }
    public abstract bool CanAccept(DragableItem item);
    
    public virtual void SetData(InventoryItemData item)
    {
        ItemVisual = DrawItem(item);
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
        instance.itemData.PageIndex = currPage;
    }

    public virtual void Init((int ,Controller) param)
    {
        Index = param.Item1;
        Owner = param.Item2;
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
        var dropped = eventData.pointerDrag;
        var dragItem = dropped.GetComponent<DragableItem>();
        SwapItems(dragItem);
    }

    public virtual void SwapItems(DragableItem dragItem)
    {
        var slots = InventorySlotsComponent.slots;


        if (dragItem.slotIndex == Index)
            return;
        int befSlot = dragItem.slotIndex;
        var trysetFirstItem = true;
        var isSetedItem = false;
        if (!IsEmpty)
        {
            trysetFirstItem = slots[dragItem.slotIndex].TrySetItem(ItemVisual);
            isSetedItem = true;
        }

        if (trysetFirstItem)
        {
            if (!TrySetItem(dragItem))
                return;
            if (!isSetedItem)
                slots[befSlot].Clear();

            DropLogic(ItemVisual, befSlot);
        }

        slots[befSlot].OldSlotFinilaizer();
    }

    public virtual void OldSlotFinilaizer()
    {
        
    }
    
    public virtual void DropLogic(DragableItem visualElement,int sourceSlotIndex)
    {
        var slots = InventorySlotsComponent.slots;
        
        var sourceSlot = slots[sourceSlotIndex];
        
    
        InventorySystem.SwapItems(sourceSlot, slots[Index], slots);
        var item = sourceSlot.GetItem();
        if (item != null)
        {
            item.parentAfterDrag = sourceSlot.transform;
            item.GetComponent<DragableItem>().slotIndex = sourceSlotIndex;
        }
    }
}

namespace Init
{
    public interface IInitializable<in T>
    {
        void Init(T param);
    }   
}