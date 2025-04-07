using Controllers;
using UnityEngine;
namespace Systems {
    public class SpriteFlipSystem : BaseSystem
    {
        SpriteFlipComponent spriteFlipComponent;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            spriteFlipComponent = owner.GetControllerComponent<SpriteFlipComponent>();
            this.owner = owner;
        }
        public override void Update() 
        {
            if (spriteFlipComponent.direction.x == -1)
            {
                owner.transform.localScale = new Vector3(-1,1,1);
            }
            else if (spriteFlipComponent.direction.x == 1)
            {
                owner.transform.localScale = new Vector3(1, 1, 1);
            }
        }
    }

    public class SpriteFlipComponent: IComponent
    {
        public Vector2 direction;
    }
}