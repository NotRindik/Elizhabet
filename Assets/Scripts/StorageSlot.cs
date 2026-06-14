using Systems;

public class StorageSlot : SlotBase
{
    public int currPage;
    
    public override bool CanAccept(DragableItem item)
    {
        return item != null;
    }
    
    public override InventorySystem.SlotRef BuildSlotRef()
        => new InventorySystem.SlotRef(isHotBar: false, Index + currPage /* * pageSize*/);
     
    public override void OnItemClick()
    {
    }
}
