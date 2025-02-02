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
                
                backpackComponent.items[0].transform.position = colorPositioning.vectorValue[0];
                backpackComponent.items[0].rb.bodyType = RigidbodyType2D.Static;
                backpackComponent.items[0].col.isTrigger = true;
                Vector2 perpendicularDirection = new Vector2(-colorPositioning.direction.y, colorPositioning.direction.x);
                Vector2 collinearDirection = -colorPositioning.direction.normalized;
                float angle = Mathf.Atan2(collinearDirection.y, collinearDirection.x) * Mathf.Rad2Deg;
                backpackComponent.items[0].transform.rotation = Quaternion.Euler(0,0, angle);
                backpackComponent.items[0].transform.localScale = new Vector3(1,owner.transform.localScale.x,1);
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