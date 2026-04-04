using Systems;

namespace Assets.Scripts
{
    public class ArmourSlot : SlotBase
    {
        public ArmourType armourType;
        public ArmourPart armourPart;

        public ArmourComponent armourComponentTemp;

        public override bool CanAccept(DragableItem item)
        {
            if(item == null) return false;

            var itemComponent = item.itemData.Item.GetItemComponent<ArmourItemComponent>();
            if(itemComponent != null)
            {
                return itemComponent.armourPart == armourPart;
            }
            return false;
        }

        public override void SwapItems(DragableItem dragableItem)
        {
            base.SwapItems(dragableItem);

            if (ItemVisual)
            {
                armourComponentTemp = ((BookController)(Owner)).player.GetControllerComponent<ArmourComponent>();

                armourComponentTemp.RemoveArmour(armourType, armourPart);
                armourComponentTemp.AddArmour(armourType, armourPart, ItemVisual.itemData.Item);
            }
        }

        public override void OldSlotFinilaizer()
        {
            if (ItemVisual == null)
            {
                armourComponentTemp.RemoveArmour(armourType, armourPart);
            }
            else
            {
                armourComponentTemp = ((BookController)(Owner)).player.GetControllerComponent<ArmourComponent>();

                armourComponentTemp.AddArmour(armourType, armourPart, ItemVisual.itemData.Item);
            }
        }

        public override void OnItemClick()
        {
            base.OnItemClick();

            var input = Owner.GetControllerSystem<IInputProvider>();
            if (input.GetState().FastPress.IsPressed)
            {
                for (int i = 0; i < InventorySlotsComponent.storageSlots.Length; i++)
                {
                    ItemVisual.transform.SetParent(ItemVisual.transform.root);
                    ItemVisual.transform.SetAsLastSibling();
                    if (InventorySlotsComponent.storageSlots[i].IsEmpty)
                    {
                        InventorySlotsComponent.storageSlots[i].SwapItems(ItemVisual);
                        return;
                    }
                    ItemVisual.transform.SetParent(transform);
                }
            }
        }
    }
}