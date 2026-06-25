using UnityEngine;

public class HotSlots : SlotBase
{
    protected override bool IsBeltSlot => true;
    public override bool CanAccept(DragableItem item) => true;
}
