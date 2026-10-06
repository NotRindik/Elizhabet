using System.Linq;
using NaughtyAttributes;
using UnityEngine;

namespace Systems
{
    internal class Armour : Item
    {
        protected override IComponent[] DefaultComponents =>
            base.DefaultComponents
                .Concat(new IComponent[]
                {
                    new ArmourItemComponent()
                })
                .ToArray();
    }

    [System.Serializable]
    public class ArmourItemComponent : IComponent, ISaveSerialize
    {
        public Sprite armourSprite;
        
        public PrefabViewData[] prefabViewData = {};
        
        public ArmourPart armourPart;
        [SaveField] public bool isEquiped;
        public float protection;
        
        public void ApplyConfig(AbstractEntity config)
        {
            ArmourItemComponent armour = config.GetControllerComponentDirect<ArmourItemComponent>();
            if (armour != null)
            {
                armourSprite = armour.armourSprite;
                armourPart = armour.armourPart;
                protection = armour.protection;
                prefabViewData = armour.prefabViewData;
            }
        }
        
        [System.Serializable]
        public class PrefabViewData
        {
            public GameObject prefab;
            public ColorPosNameConst nameConst;
            public Vector2 offset;
            public bool rotateByDirection;
            public float rotationOffset;
            public int SortingLayer;
        }
    }
}
