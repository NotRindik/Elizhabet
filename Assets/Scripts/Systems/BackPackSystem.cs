using Controllers;
using System.Collections.Generic;
using UnityEngine;

namespace Systems
{
    public class BackPackSystem : BaseSystem
    {
        BackpackComponent backpackComponent;
        public int currentItem;
        ColorPositioningComponent colorPositioning;
        public void Initialize(Controller owner, BackpackComponent backpackComponent, ColorPositioningComponent colorPositioning)
        {
            base.Initialize(owner);
            this.backpackComponent = backpackComponent;
            this.colorPositioning = colorPositioning;
        }

        public override void Update()
        {

            if (backpackComponent.items.Count > 0)
            {
                backpackComponent.items[currentItem].TakeUp();
            }
        }

        public void GetPerpendicular()
        {
            Vector2 perpendicularDirection = new Vector2(-colorPositioning.direction.y, colorPositioning.direction.x);
            float angle = Mathf.Atan2(perpendicularDirection.y, perpendicularDirection.x) * Mathf.Rad2Deg;
        }
    }

    [System.Serializable]
    public class BackpackComponent: IComponent
    {
        public List<Items> items = new List<Items>();
    }
}