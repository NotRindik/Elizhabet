using Controllers;
using UnityEngine;

namespace Systems
{
    public class FrictionSystem : BaseSystem
    {
        private MoveComponent _moveComponent;

        protected EntityController entity;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            entity = (EntityController)base.owner;
            _moveComponent = base.owner.GetControllerComponent<MoveComponent>();
            entity.OnUpdate += Friction;
        }
        public override void OnUpdate()
        {
            base.OnUpdate();
        }

        void Friction()
        {
            if (Mathf.Abs(_moveComponent.direction.x) == 0)
            {
                float amount = Mathf.Min(Mathf.Abs(entity.baseFields.rb.linearVelocityX), Mathf.Abs(_moveComponent.frictionAmount));

                amount *= Mathf.Sign(entity.baseFields.rb.linearVelocityX);
                entity.baseFields.rb.AddForce(Vector2.right * -amount, ForceMode2D.Impulse);
            }
        }
    }
}