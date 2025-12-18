using Controllers;
using UnityEngine;

namespace Systems
{
    public class MoveSystem : BaseSystem
    {
        private MoveComponent moveComponent;
        private ControllersBaseFields baseFields;

        public override void Initialize(IController owner)
        {
            base.Initialize(owner);
            this.owner = (EntityController)owner;
            moveComponent = owner.GetControllerComponent<MoveComponent>();
            baseFields = owner.GetControllerComponent<ControllersBaseFields>();
            if(moveComponent.autoUpdate)
                this.owner.OnFixedUpdate += Update;
        }
        public override void OnUpdate()
        {
            Vector2 moveDir = (Vector2) transform.right.normalized;

            float currentSpeed = Vector2.Dot(baseFields.rb.linearVelocity, moveDir);

            float targetSpeed = moveComponent.direction.x * moveComponent.speed * moveComponent.speedMultiplierDynamic;

            float speedDif = targetSpeed - currentSpeed;
            float accelRate = Mathf.Abs(targetSpeed) > 0.01f ? moveComponent.acceleration : moveComponent.decceleration;

            float movement = Mathf.Pow(Mathf.Abs(speedDif) * accelRate, moveComponent.velPower) * Mathf.Sign(speedDif);

            baseFields.rb.AddForce(moveDir * movement);
        }
    }
    [System.Serializable]
    public class MoveComponent : IComponent
    {
        public Vector2 direction;
        public float speed;
        public float speedMultiplierDynamic;
        public float frictionAmount;
        public float acceleration;
        public float decceleration;
        public float velPower;
        public bool autoUpdate = false;
    }
}
