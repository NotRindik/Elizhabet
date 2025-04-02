using Controllers;
using UnityEngine;

namespace Systems
{
    public class MoveSystem : BaseSystem
    {
        private MoveComponent moveComponent;
        private EntityController owner;

        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            this.owner = (EntityController)owner;
            moveComponent = owner.GetControllerComponent<MoveComponent>();
        }
        public override void Update()
        {
            float targetSpeed = moveComponent.direction.x * moveComponent.speed * moveComponent.speedMultiplierDynamic;
            float speedDif = targetSpeed - owner.baseFields.rb.linearVelocityX;
            float accelRate = Mathf.Abs(targetSpeed) > 0.01f ? moveComponent.acceleration : moveComponent.decceleration;
            float movement = Mathf.Pow(Mathf.Abs(speedDif) * accelRate, moveComponent.velPower) * Mathf.Sign(speedDif);
            owner.baseFields.rb.AddForce(Vector2.right * (movement));
            Friction();
        }
        void Friction()
        {
            if (Mathf.Abs(moveComponent.direction.x) < 0.01f)
            {
                float amount = Mathf.Min(Mathf.Abs(owner.baseFields.rb.linearVelocityX), Mathf.Abs(moveComponent.frictionAmount));

                amount *= Mathf.Sign(owner.baseFields.rb.linearVelocityX);

                owner.baseFields.rb.AddForce(Vector2.right * -amount, ForceMode2D.Impulse);
            }
        }
    }
    [System.Serializable]
    public class MoveComponent : IComponent
    {
        internal Vector2 direction;
        public float speed;
        public float speedMultiplierDynamic;
        public float frictionAmount;
        public float acceleration;
        public float decceleration;
        public float velPower;
    }
}
