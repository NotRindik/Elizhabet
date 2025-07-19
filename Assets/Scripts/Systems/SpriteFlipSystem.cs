using Controllers;
using UnityEngine;
namespace Systems {
    public class SpriteFlipSystem : BaseSystem
    {
        SpriteFlipComponent spriteFlipComponent;
        WallEdgeClimbComponent _wallEdgeClimbComponent;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            spriteFlipComponent = owner.GetControllerComponent<SpriteFlipComponent>();
            _wallEdgeClimbComponent = base.owner.GetControllerComponent<WallEdgeClimbComponent>();
            owner.OnUpdate += Update;
            this.owner = owner;
        }
        public override void OnUpdate() 
        {
            if (_wallEdgeClimbComponent != null)
            {
                if (_wallEdgeClimbComponent.EdgeStuckProcess != null)
                {
                    return;
                }
            }
            
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
    [System.Serializable]
    public class SpriteFlipComponent: IComponent
    {
        public Vector2 direction;
    }
}