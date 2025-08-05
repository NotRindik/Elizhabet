using UnityEngine;

namespace Systems
{
    internal class Armour : Item
    {
        public ArmourItemComponent armourItemComponent;
    }
    [System.Serializable]
    public class ArmourItemComponent : IComponent
    {
        public Sprite armourSprite;
        public ArmourPart armourPart;
        public bool isEquiped;
    }
}
