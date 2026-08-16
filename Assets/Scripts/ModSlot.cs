using UnityEngine;
using UnityEngine.UI;

public class ModSlot : SlotBase
{
    public bool isSlotActive;

    public Image slotBlockImage;
    public Sprite blockSprite;

    public override bool CanAccept(DragableItem item)
    {
        return isSlotActive; //item.itemData.Item.GetItemComponentFromConfig<A>()
    } //TODO кароче ты потом сделаешь класс для модов
}
