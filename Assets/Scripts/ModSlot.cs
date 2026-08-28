public class ModSlot : SlotBase
{
    public ModificationBodyParts  modificationBodyParts;
    
    public override bool CanAccept(DragableItem item)
    {
        var modItem = item.itemData.Item.GetItemComponent<ModificatorItemComponent>();
        return modItem != null && modItem.modificationBodyParts == modificationBodyParts;
    }
    
    public override void OnItemClick()
    {
        base.OnItemClick();

        var input = Owner.GetControllerSystem<IInputProvider>();
        if (!input.GetState().FastPress.IsPressed) return;

        foreach (var storageSlot in InventorySlotsComponent.storageSlots)
        {
            if (!storageSlot.IsEmpty) continue;

            ItemVisual.transform.SetParent(ItemVisual.transform.root);
            ItemVisual.transform.SetAsLastSibling();
            storageSlot.SwapItems(ItemVisual);
            return;
        }
    }
}

public enum ModificationBodyParts
{
    Brain,
    Arm, 
    KneeCup,
    Leg,
    Breast
}
