using Controllers;
using System;
using UnityEngine;
namespace Systems {
    public class SpriteFlipSystem : BaseSystem
    {
        SpriteFlipComponent spriteFlipComponent;
        WallEdgeClimbComponent _wallEdgeClimbComponent;

        public override bool IsActive
        {
            get => isActive;
            set
            {
                isActive = value;
            }
        }

        public override void Initialize(AbstractEntity owner)
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
            
            if (spriteFlipComponent.direction == -1)
            {
                transform.SetFacing(-1f);
                spriteFlipComponent.OnFlip?.Invoke(new Vector3(-1f, 1f, 1f));
            }
            else if (spriteFlipComponent.direction == 1)
            {
                transform.SetFacing(1f);
                spriteFlipComponent.OnFlip?.Invoke(new Vector3(1f, 1f, 1f));
            }
        }
    }
    [System.Serializable]
    public class SpriteFlipComponent: IComponent
    {
        public int direction;
        public Action<Vector3> OnFlip;
        
        public bool IsFlip => direction == -1;
    }
}