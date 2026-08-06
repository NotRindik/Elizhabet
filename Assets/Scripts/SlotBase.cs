using System;
using Controllers;
using Init;
using Systems;
using UnityEngine;
using UnityEngine.EventSystems;
public abstract class SlotBase : MonoBehaviour,IInitializable<(int,AbstractEntity)>,IDropHandler
{
    protected virtual bool IsBeltSlot => false;
        
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
    public virtual int Index { get; protected set; }

    public Action<SlotBase, DragableItem> OnDropAction;
    public Action OnDropCompleted;
    public Action OnDropFailed;
    public abstract bool CanAccept(DragableItem item);
    
    public virtual void SetData(InventoryItemData item)
    {
        ItemVisual = DrawItem(item);
        ItemVisual?.SetVisualContext(IsBeltSlot);
    }
    public virtual bool TrySetItem(DragableItem item)
    {
        if (CanAccept(item))
        {
            ItemVisual = item;
            ItemVisual.parentAfterDrag = transform;
            ItemVisual.sourceSlot = this;
            ItemVisual.transform.SetAsLastSibling();
            
            item.slotIndex = Index;
            UpdateItemData(item);
            ItemVisual.SetVisualContext(IsBeltSlot); 
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
            transform,
            false
        );
        instance.slotIndex = Index;

        instance.itemData = item;
        UpdateItemData(instance);

        var itemComponent = item.Item.GetItemComponentFromConfig<ItemComponent>();
        
        instance.image.sprite = itemComponent?.itemIcon;
        instance.image.color = Color.white;

        instance.parentAfterDrag = transform;
        
        instance.sourceSlot = this;
        instance.SetVisualContext(this is HotSlots); 
        
        return instance;
    }

    private void UpdateItemData(DragableItem instance)
    {
        instance.itemData.SlotIndex = Index;
        instance.itemData.PageIndex = currPage;
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
    }
    
    public virtual void OnDrop(PointerEventData eventData)
    {
        Debug.Log("On Dropped");
        var dropped = eventData.pointerDrag;
        var dragItem = dropped.GetComponent<DragableItem>();
        SwapItems(dragItem);
    }

    public virtual void SwapItems(DragableItem dragItem)
    {
        bool dropRes = false;
        if (dragItem.sourceSlot == this)
        {
            OnDropFailed?.Invoke();
            return;
        }


        var trysetFirstItem = true;
        var isSetedItem = false;

        if (!IsEmpty && CanAccept(dragItem))
        {
            trysetFirstItem = dragItem.sourceSlot.TrySetItem(ItemVisual);
            isSetedItem = true;
        }

        if (trysetFirstItem)
        {
            var sourceSlotTemp = dragItem.sourceSlot;
            if (!TrySetItem(dragItem))
            {
                OnDropFailed?.Invoke();
                return;
            }
            
            if (!isSetedItem)
                sourceSlotTemp.Clear();
            dropRes = true;
            
            DropLogic(ItemVisual, sourceSlotTemp);
        }
        
        OnDropCompleted?.Invoke();
        dragItem.sourceSlot.OldSlotFinilaizer();
    }

    public virtual void OldSlotFinilaizer()
    {
    }
    
    public virtual void DropLogic(DragableItem visualElement,SlotBase sourceSlot)
    {
        InventorySystem.SwapOrMoveItems(sourceSlot.GetSlotRef(), GetSlotRef());
        var item = sourceSlot.GetItem();
        if (item != null)
        {
            item.parentAfterDrag = sourceSlot.transform;
            
            item.sourceSlot = sourceSlot;
        }

        OnDropAction?.Invoke(this,visualElement);
    }

    public virtual SlotRef GetSlotRef()
    {
        return InventoryComponent.GetSlotRef(Index);
    }
}

namespace Init
{
    public interface IInitializable<in T>
    {
        void Init(T param);
    }   
}