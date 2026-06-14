using System.Linq;
using Systems;
using UnityEngine.EventSystems;
public class ArmourSlot : SlotBase
{
    public ArmourType armourType;
    public ArmourPart armourPart;

    public override bool CanAccept(DragableItem item)
    {
        if (item == null) return false;
        var armour = item.itemData.Item.GetItemComponent<ArmourItemComponent>();
        return armour != null && armour.armourPart == armourPart;
    }

    public override InventorySystem.SlotRef BuildSlotRef()
        => new InventorySystem.SlotRef(isHotBar: false, Index); // или отдельный тип если добавишь EquipSlot

    public override void OnDrop(PointerEventData eventData)
    {
        var dragItem = eventData.pointerDrag?.GetComponent<DragableItem>();
        if (dragItem == null || !CanAccept(dragItem)) return;

        bool hadItem = !IsEmpty;  // запоминаем ДО move

        InventorySystem.MoveOrSwap(dragItem.SourceSlot, BuildSlotRef());

        var armourComp = ((BookController)Owner).player.GetControllerComponent<ArmourComponent>();
        if (hadItem)
            armourComp.RemoveArmour(armourType, armourPart);

        armourComp.AddArmour(armourType, armourPart, dragItem.itemData.Item);
    }

    public override void OnItemClick()
    {
        if (!Owner.GetControllerSystem<IInputProvider>().GetState().FastPress.IsPressed) return;
        if (IsEmpty) return;

        var inv = InventoryComponent;
        int freeIndex = inv.storage.items.FindIndex(s => s == null);
        if (freeIndex == -1) freeIndex = inv.storage.items.Count;

        var armourComp = ((BookController)Owner).player.GetControllerComponent<ArmourComponent>();
        armourComp.RemoveArmour(armourType, armourPart);

        InventorySystem.MoveOrSwap(
            BuildSlotRef(),
            new InventorySystem.SlotRef(isHotBar: false, freeIndex)
        );
    }
}