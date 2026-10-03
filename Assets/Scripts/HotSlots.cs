using Systems;
using UnityEngine;

public class HotSlots : SlotBase
{
    protected override bool IsBeltSlot => true;
    public override bool CanAccept(DragableItem item) => true;

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
        
        if (InventoryComponent.IsStorageUnlocked)
            TryFastMove(InventorySlotsComponent.storageSlots, visual);
    }
}