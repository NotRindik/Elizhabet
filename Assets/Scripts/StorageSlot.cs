using System.Linq;
using Systems;

public class StorageSlot : SlotBase
{
    public int BoundStorageIndex = -1;

    public int GlobalIndex
    {
        get
        {
            int storageOffset = InventoryComponent.hotBar.Count
                                + InventoryComponent.armor.Count
                                + InventoryComponent.accessories.Count;
            
            int realIndex = BoundStorageIndex >= 0
                ? BoundStorageIndex
                : InventoryComponent.storage.Raw.Count;

            return storageOffset + realIndex;
        }
    }
    

    public override bool CanAccept(DragableItem item)
    {
        if (item == null) return false;
        if (!InventoryComponent.storage.CanAdd(item.itemData.Item  ))
            return false;

        return !InventoryComponent.storage.Raw.Contains(item.itemData.Item);
    }

    public void AttachExisting(DragableItem item)
    {
        ItemVisual = item;
        ItemVisual.parentAfterDrag = transform;
        ItemVisual.transform.SetAsLastSibling();

        item.slotIndex = Index;
        item.itemData.SlotIndex = Index;
        item.itemData.PageIndex = currPage;
        item.SetVisualContext(IsBeltSlot); 
    }

    public override SlotRef GetSlotRef()
    {
        return InventoryComponent.GetSlotRef(GlobalIndex);
    }

    public override void OnItemClick()
    {
        base.OnItemClick();

        var input = Owner.GetControllerSystem<IInputProvider>();
        if (!input.GetState().FastPress.IsPressed) return;

        var visual = ItemVisual;
        if (visual == null) return;

        bool isArmour = visual.itemData.Item.GetItemComponent<ArmourItemComponent>() != null;
        
        if (isArmour && TryFastMove(InventorySlotsComponent.armourSlots, visual))
            return;
        
        TryFastMove(InventorySlotsComponent.hotSlots, visual);
    }
}