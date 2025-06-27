using Controllers;
using Systems;
using UnityEngine;

namespace States
{
    public class AttackState:IState
    {
        private Controller _controller;
        private AttackSystem _attackSystem;
        private SpriteFlipSystem _spriteFlipSystem;
        private ControllersBaseFields _baseFields;
        private ColorPositioningComponent _colorPositioning;
        private GroundingComponent _groundingComponent;
        private MoveComponent _moveComponent;
        private float speedTemp;
        private MoveSystem _moveSystem;
        public AttackState(Controller controller)
        {
            _controller = controller;
            _attackSystem = controller.GetControllerSystem<AttackSystem>();
            _spriteFlipSystem = controller.GetControllerSystem<SpriteFlipSystem>();
            _moveSystem = controller.GetControllerSystem<MoveSystem>();
            _baseFields = controller.GetControllerComponent<ControllersBaseFields>();
            _groundingComponent = controller.GetControllerComponent<GroundingComponent>();
            _moveComponent = controller.GetControllerComponent<MoveComponent>();
            _colorPositioning = controller.GetControllerComponent<ColorPositioningComponent>();
            _spriteFlipSystem.IsActive = false;
        }

        public void Enter()
        {
            _colorPositioning.spriteRenderer.transform.rotation = Quaternion.identity;
            speedTemp = _moveComponent.speed;
            _attackSystem.Update();
        }
        public void Update()
        {
            if (_groundingComponent.isGround)
            {
                _baseFields.rb.linearVelocityX = 0;
                _moveComponent.speed = 1;
            }
            _moveSystem.Update();
        }
        public void Exit()
        {
            _moveComponent.speed = speedTemp;
            _spriteFlipSystem.IsActive = true;
        }
    }
}