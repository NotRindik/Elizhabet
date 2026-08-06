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
        if (!input.GetState().FastPress.IsPressed)
            return;

        bool isArmour = ItemVisual.itemData.Item.GetItemComponent<ArmourItemComponent>() != null;

        ItemVisual.transform.SetParent(ItemVisual.transform.root);
        ItemVisual.transform.SetAsLastSibling();

        if (isArmour)
        {
            for (int i = 0; i < InventorySlotsComponent.armourSlots.Length; i++)
            {
                if (InventorySlotsComponent.armourSlots[i].IsEmpty
                    && InventorySlotsComponent.armourSlots[i].CanAccept(ItemVisual))
                {
                    InventorySlotsComponent.armourSlots[i].SwapItems(ItemVisual);
                    return;
                }
                
                InventorySlotsComponent.armourSlots[i].OnDropFailed?.Invoke();
            }
        }
        else
        {
            for (int i = 0; i < InventorySlotsComponent.storageSlots.Length; i++)
            {
                if (InventorySlotsComponent.storageSlots[i].IsEmpty && InventorySlotsComponent.storageSlots[i].CanAccept(ItemVisual))
                {
                    InventorySlotsComponent.storageSlots[i].SwapItems(ItemVisual);
                    return;
                }
                
                InventorySlotsComponent.storageSlots[i].OnDropFailed?.Invoke();
            }
        }

        ItemVisual.transform.SetParent(transform);
    }
}