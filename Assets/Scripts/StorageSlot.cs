using System.Linq;
using Systems;

public class StorageSlot : SlotBase
{
    public int GlobalIndex
    {
        get
        {
            int storageOffset = InventoryComponent.hotBar.Count + InventoryComponent.armor.Count + InventoryComponent.accessories.Count;
            return storageOffset + Index;
        }   
    }

    public override bool CanAccept(DragableItem item)
    {
        if (item == null) return false;

        // нельзя "свапнуть" предмет, который сам уже физически в сторадже —
        // это не обмен, грид сам управляет позициями через компакцию
        return !InventoryComponent.storage.Raw.Contains(item.itemData.Item);
    }

    public void AttachExisting(DragableItem item)
    {
        ItemVisual = item;
        ItemVisual.parentAfterDrag = transform; // запустит плавный слайд в новую позицию
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

            ItemVisual.transform.SetParent(transform);
        }
    }
}