using Controllers;
using System.Collections.Generic;

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
        public List<ItemComponent> items = new List<ItemComponent>();
        public int currentItem = 0;
    }
}