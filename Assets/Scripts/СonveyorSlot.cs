
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public class СonveyorSlot : SlotBase
{
    public override bool CanAccept(DragableItem item)
    {
        return true;
    }
    public override void Clear()
    {
        base.Clear();
        
        if(Index < 5)
            SwapItems(InventorySlotsComponent.conveyorSlots[Index+1].GetItem().gameObject);
    }
    public override void OnDrop(PointerEventData eventData)
    {
        var dropped = eventData.pointerDrag;
        SwapItems(dropped);
    }

    public override bool TrySetItem(DragableItem item)
    {
        if (CanAccept(item))
        {
            ItemVisual = item;
            if (item == null)
            {
                return true;
            }
            ItemVisual.parentAfterDrag = transform;
            ItemVisual.transform.SetParent(transform);
            ItemVisual.transform.SetAsLastSibling();
            
            DropLogic(item);
            
            item.slotIndex = Index;
            return true;
        }
        return false;
    }
    
    public void SwapItems(GameObject dropped)
    {
        var dragItem = dropped.GetComponent<DragableItem>();

        if (dragItem.slotIndex == Index)
            return;
        int befIndex = dragItem.slotIndex;
        InventorySlotsComponent.conveyorSlots[dragItem.slotIndex].TrySetItem(ItemVisual);
        
        if (!TrySetItem(dragItem))
            return;
        
        MoveNearItemsToNextSlot(befIndex);
        MoveItemToNextSlot(dropped);
    }
    private void MoveNearItemsToNextSlot(int befIndex)
    {
        var upIndex = befIndex + 1;
        if (upIndex < 5 && upIndex != 0)
        {
            if (!InventorySlotsComponent.conveyorSlots[upIndex].IsEmpty)
            {
                InventorySlotsComponent.conveyorSlots[befIndex].SwapItems(InventorySlotsComponent.conveyorSlots[upIndex].GetItem().gameObject);
            }
        }
    }
    private void MoveItemToNextSlot(GameObject dropped)
    {
        var lessIndex = Index - 1;
        if (lessIndex >= 0)
        {
            if (InventorySlotsComponent.conveyorSlots[lessIndex].IsEmpty)
                InventorySlotsComponent.conveyorSlots[lessIndex].SwapItems(dropped);
        }
    }
}