using Systems;

public class StorageSlot : SlotBase
{
    public override bool CanAccept(DragableItem item)
    {
        return item != null;
    }

    public override void OnItemClick()
    {
        base.OnItemClick();

        var input = Owner.GetControllerSystem<IInputProvider>();
        if (input.GetState().FastPress.IsPressed)
        {
            bool isArmour = ItemVisual.itemData.Item.GetItemComponent<ArmourItemComponent>() != null;

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


            for (int i = 0; i < InventorySlotsComponent.conveyorSlots.Length; i++)  
            {
                if (InventorySlotsComponent.conveyorSlots[i].IsEmpty)
                {
                    InventorySlotsComponent.conveyorSlots[i].SwapItems(ItemVisual);
                    return;
                }
            }

            ItemVisual.transform.SetParent(transform);
        }
    }
}
