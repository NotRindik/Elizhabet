using UnityEngine;
namespace Systems
{
    public class MovementAnimationSystem : BaseAnimationSystem
    {
        protected MoveComponent moveComponent;
        protected JumpComponent jumpComponent;
        public void Initialize(AnimationComponent animationComponent, MoveComponent moveComponent,JumpComponent jumpComponent)
        {
            this.animationComponent = animationComponent;
            this.moveComponent = moveComponent;
            this.jumpComponent = jumpComponent;
        }
        public override void Update()
        {
            base.Update();
        }

        public override string GetState()
        {
            if (!jumpComponent.isGround && animationComponent.rigidbody.linearVelocityY < 0)
                return "FallDown";
            else if (!jumpComponent.isGround && animationComponent.rigidbody.linearVelocityY > 0)
            {
                return "FallUp";
            }
            if (moveComponent.direction.x != 0 && Mathf.Abs(animationComponent.rigidbody.linearVelocityX) > 1)
                return "Walk";

            return "Idle";
        }
    }
}