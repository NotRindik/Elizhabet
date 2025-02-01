using Controllers;
using UnityEngine;
namespace Systems {
    public class SpriteFlipSystem : BaseSystem
    {
        SpriteFlipComponent spriteFlipComponent;
        public void Initialize(Controller owner,SpriteFlipComponent spriteFlipComponent)
        {
            this.spriteFlipComponent = spriteFlipComponent;
            this.owner = owner;
        }
        public override void Update() 
        {
            if (spriteFlipComponent.direction.x == -1)
            {
                owner.transform.rotation = Quaternion.Euler(owner.transform.rotation.x,-180f, owner.transform.rotation.z);
            }
            else if (spriteFlipComponent.direction.x == 1)
            {
                owner.transform.rotation = Quaternion.Euler(owner.transform.rotation.x, 0, owner.transform.rotation.z);
            }
        }
    }

    public class SpriteFlipComponent: IComponent
    {
        public Vector2 direction;
    }
}