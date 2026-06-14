using Systems;

public class HotBarSlot : SlotBase
{
    public override bool CanAccept(DragableItem item) => true;
    
    public override InventorySystem.SlotRef BuildSlotRef() 
        => new InventorySystem.SlotRef(isHotBar: true, Index);
    
    public override void OnItemClick()
    {
        if (!Owner.GetControllerSystem<IInputProvider>().GetState().FastPress.IsPressed) return;
        if (IsEmpty) return;

        // Находим первый свободный индекс в storage
        var inv = InventoryComponent;
        int freeIndex = inv.storage.items.FindIndex(s => s == null);
        if (freeIndex == -1) freeIndex = inv.storage.items.Count;

        InventorySystem.MoveOrSwap(
            BuildSlotRef(),
            new InventorySystem.SlotRef(isHotBar: false, freeIndex)
        );
    }
}