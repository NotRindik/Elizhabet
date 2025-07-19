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
            if(moveComponent.autoUpdate)
                this.owner.OnUpdate += Update;
        }
        public override void OnUpdate()
        {
            // Локальное направление: transform.right (или .up, если хочешь вертикальное)
            Vector2 moveDir = (Vector2)owner.transform.right.normalized;

            // Скорость вдоль направления
            float currentSpeed = Vector2.Dot(owner.baseFields.rb.linearVelocity, moveDir);

            // Желаемая скорость
            float targetSpeed = moveComponent.direction.x * moveComponent.speed * moveComponent.speedMultiplierDynamic;

            // Разница между текущей и целевой скоростью
            float speedDif = targetSpeed - currentSpeed;

            // Выбираем ускорение или замедление
            float accelRate = Mathf.Abs(targetSpeed) > 0.01f ? moveComponent.acceleration : moveComponent.decceleration;

            // Считаем силу
            float movement = Mathf.Pow(Mathf.Abs(speedDif) * accelRate, moveComponent.velPower) * Mathf.Sign(speedDif);

            // Применяем силу в направлении движения
            owner.baseFields.rb.AddForce(moveDir * movement);
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
