using Controllers;
using System.Collections.Generic;
using UnityEngine;

namespace Systems
{
    public class BackPackSystem : BaseSystem
    {
        BackpackComponent backpackComponent;
        ColorPositioningComponent colorPositioning;
        public void Initialize(Controller owner, BackpackComponent backpackComponent, ColorPositioningComponent colorPositioning)
        {
            base.Initialize(owner);
            this.backpackComponent = backpackComponent;
            this.colorPositioning = colorPositioning;
        }

        public override void Update()
        {
        }
    }

    [System.Serializable]
    public class BackpackComponent: IComponent
    {
        public List<Items> items = new List<Items>();
        public int currentItem;
    }
}