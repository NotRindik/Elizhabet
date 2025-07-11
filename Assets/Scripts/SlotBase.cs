using System;
using Controllers;
using Init;
using Systems;
using UnityEngine;
using UnityEngine.EventSystems;

public abstract class SlotBase : MonoBehaviour,IInitializable<(int,Controller)>,IDropHandler
{
    protected DragableItem ItemVisual;
    protected Controller Owner;
    protected InventorySystem InventorySystem;
    protected InventoryComponent InventoryComponent;
    protected InventorySlotsComponent InventorySlotsComponent;
    public int Index { get; protected set; }
    public abstract bool CanAccept(DragableItem item);

    public ref ItemStack GetData()
    {
        if (ItemVisual == null)
            throw new NullReferenceException("ItemVisual is null");
        return ref ItemVisual.itemData;
    }
    public virtual void SetData(ItemStack item)
    {
        ItemVisual = DrawItem(item);
    }
    public virtual bool TrySetItem(DragableItem item)
    {
        if (CanAccept(item))
        {
            ItemVisual = item;
            if (item == null)
                return true;
            ItemVisual.parentAfterDrag = transform;
            ItemVisual.transform.SetParent(transform);
            ItemVisual.transform.SetAsLastSibling();
            
            DropLogic(item);
            
            item.slotIndex = Index;
            return true;
        }
        return false;
    }
    public DragableItem GetItem() => ItemVisual;
    public virtual void Clear()
    {
        if (ItemVisual)
        {
            Destroy(ItemVisual.gameObject);
        }
        ItemVisual = null;
    }

    public bool IsEmpty => GetItem() == null;

    protected DragableItem DrawItem(ItemStack item)
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        if (item == null || item.Count == 0)
            return null;

        var instance = Instantiate(
            Owner.GetControllerComponent<InventorySlotsComponent>().itemPrefab,
            transform.position,
            Quaternion.identity
        );
        instance.slotIndex = Index;
        instance.itemData = item;
        var itemComponent = item.GetItemComponent<ItemComponent>();
        instance.image.sprite = itemComponent?.itemIcon;
        instance.image.color = Color.white;
        instance.transform.SetParent(transform, false);
        instance.parentAfterDrag = transform;
        instance.transform.position = transform.position;
        return instance;
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
    
    public virtual void OnDrop(PointerEventData eventData)
    {
        var dropped = eventData.pointerDrag;
        var dragItem = dropped.GetComponent<DragableItem>();
        var slots = InventorySlotsComponent.slots;


        if (dragItem.slotIndex == Index)
            return;
        
        slots[dragItem.slotIndex].TrySetItem(ItemVisual);
        
        if (!TrySetItem(dragItem))
            return;
    }
    
    public virtual void DropLogic(DragableItem visualElement)
    {
        var slots = InventorySlotsComponent.slots;
        
        Transform sourceSlot = slots[visualElement.slotIndex].transform;
        
    
        InventorySystem.SwapItems(slots[visualElement.slotIndex], slots[Index], slots);
    
        if (sourceSlot.childCount > 0)
        {
            Transform existingItem = transform.GetChild(0);
            existingItem.SetParent(sourceSlot);
            existingItem.GetComponent<DragableItem>().slotIndex = visualElement.slotIndex;
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