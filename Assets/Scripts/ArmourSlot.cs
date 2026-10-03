using Systems;

public class ArmourSlot : SlotBase
{
    public ArmourType armourType;
    public ArmourPart armourPart;

    protected override bool IsBeltSlot => false;

    public override bool CanAccept(DragableItem item)
    {
        if (item == null) return false;

        var armourComp = item.itemData.Item.GetItemComponent<ArmourItemComponent>();
        return armourComp != null && armourComp.armourPart == armourPart;
    }

    public override void OnItemClick()
    {
        base.OnItemClick();

        var input = Owner.GetControllerSystem<IInputProvider>();
        if (!input.GetState().FastPress.IsPressed) return;

        var visual = ItemVisual;
        if (visual == null) return;
        
        if (InventoryComponent.IsStorageUnlocked &&
            TryFastMove(InventorySlotsComponent.storageSlots, visual))
            return;
        
        TryFastMove(InventorySlotsComponent.hotSlots, visual);
    }
}
