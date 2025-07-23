using Systems;
using UnityEngine.EventSystems;
namespace Assets.Scripts
{
    public class ArmourSlot : SlotBase
    {
        public ArmourType armourType;
        public ArmourPart armourPart;

        public ArmourComponent armourComponentTemp;

        public override bool CanAccept(DragableItem item)
        {
            if(item == null) return true;

            var itemComponent = item.itemData.GetItemComponent<ArmourItemComponent>();
            if(itemComponent != null)
            {
                return itemComponent.armourPart == armourPart;
            }
            return false;
        }

        public override void OnDrop(PointerEventData eventData)
        {
            base.OnDrop(eventData);

            if (ItemVisual)
            {
                armourComponentTemp = ((BookController)(Owner)).player.GetControllerComponent<ArmourComponent>();

                armourComponentTemp.AddArmour(armourType, armourPart, ItemVisual.itemData);
            }
        }

        public override void OldSlotFinilaizer()
        {
            if (ItemVisual == null)
            {
                armourComponentTemp.RemoveArmour(armourType, armourPart);
            }
        }
    }
}