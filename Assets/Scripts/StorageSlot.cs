public class StorageSlot : SlotBase
{
    public override bool CanAccept(DragableItem item)
    {
        return item != null;
    }
}
