using Controllers;
using System.Collections.Generic;
using UnityEngine;

namespace Systems
{
    public class BackPackSystem : BaseSystem
    {
        BackpackComponent backpackComponent;
        public void Initialize(Controller owner, BackpackComponent backpackComponent)
        {
            base.Initialize(owner);
            this.backpackComponent = backpackComponent;
        }

        public override void Update()
        {

        }
    }

    public class BackpackComponent: IComponent
    {
        public List<Items> items = new List<Items>();
    }
}