
using System.Linq;
using Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using static UnityEditor.Progress;

public class СonveyorSlot : SlotBase
{
    public override bool CanAccept(DragableItem item)
    {
        return item != null;
    }
    public override void DestroyVisual()
    {
        base.DestroyVisual();
        
        if(Index < 5)
            if(InventorySlotsComponent.slots[Index+1].GetItem() != null)
                SwapItems(InventorySlotsComponent.slots[Index+1].GetItem());
    }
    public override void OnDrop(PointerEventData eventData)
    {
        var dropped = eventData.pointerDrag;
        var dragItem = dropped.GetComponent<DragableItem>();
        SwapItems(dragItem);
    }

    public override bool TrySetItem(DragableItem item)
    {
        if (CanAccept(item))
        {
            ItemVisual = item;
            ItemVisual.parentAfterDrag = transform;
            ItemVisual.transform.SetAsLastSibling();
            
            item.slotIndex = Index;
            return true;
        }
        return false;
    }
    
    public override void SwapItems(DragableItem dragItem)
    {

        if (dragItem.slotIndex == Index)
            return;
        int befIndex = dragItem.slotIndex;
        var trysetItem = true;
        var isItemSeted = false;
        bool isEmptySave = IsEmpty;
        if (!IsEmpty)
        {
            trysetItem = InventorySlotsComponent.slots[dragItem.slotIndex].TrySetItem(ItemVisual);
            isItemSeted = true;
        }


        if (trysetItem)
        {
            if (!TrySetItem(dragItem))
                return;
            if(!isItemSeted) 
                InventorySlotsComponent.slots[befIndex].Clear();
            DropLogic(ItemVisual, befIndex);
        }


        if (isEmptySave)
        {
            MoveNearItemsToNextSlot(befIndex);
            MoveItemToNextSlot(dragItem);
        }
        InventorySlotsComponent.slots[befIndex].OldSlotFinilaizer();
    }

    public override void OldSlotFinilaizer()
    {
        MoveNearItemsToNextSlot(Index);
        base.OldSlotFinilaizer();
    }
    public override void OnItemClick()
    {
        base.OnItemClick();

        var input = Owner.GetControllerSystem<IInputProvider>();
        if (input.GetState().FastPress.IsPressed)
        {
            bool isArmour = ItemVisual.itemData.GetItemComponent<ArmourItemComponent>() != null;
            ItemVisual.transform.SetParent(ItemVisual.transform.root);
            ItemVisual.transform.SetAsLastSibling();
            if (isArmour)
            {
                for (int i = 0; i < InventorySlotsComponent.armourSlots.Length; i++)
                {
                    if (InventorySlotsComponent.armourSlots[i].IsEmpty)
                    {
                        if (InventorySlotsComponent.armourSlots[i].CanAccept(ItemVisual))
                        {
                            InventorySlotsComponent.armourSlots[i].SwapItems(ItemVisual);
                            return;
                        }
                    }
                }
            }


            for (int i = 0; i < InventorySlotsComponent.storageSlots.Length; i++)
            {
                if (InventorySlotsComponent.storageSlots[i].IsEmpty)
                {
                    InventorySlotsComponent.storageSlots[i].SwapItems(ItemVisual);
                    return;
                }
            }

            ItemVisual.transform.SetParent(transform);
        }
    }
    private void MoveNearItemsToNextSlot(int befIndex)
    {
        var upIndex = befIndex + 1;
        if (upIndex < 5 && upIndex != 0)
        {
            if (!InventorySlotsComponent.conveyorSlots[upIndex].IsEmpty)
            {
                if (InventorySlotsComponent.conveyorSlots[befIndex].IsEmpty) 
                    InventorySlotsComponent.conveyorSlots[befIndex].SwapItems(InventorySlotsComponent.conveyorSlots[upIndex].GetItem());
            }
        }
    }
    private void MoveItemToNextSlot(DragableItem dropped)
    {
        var lessIndex = Index - 1;
        if (lessIndex >= 0)
        {
            if (InventorySlotsComponent.conveyorSlots[lessIndex].IsEmpty)
                InventorySlotsComponent.conveyorSlots[lessIndex].SwapItems(dropped);
        }
    }
}