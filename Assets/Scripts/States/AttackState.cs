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
        }

        public void Enter()
        {
            _attackSystem.Update();
        }
        public void FixedUpdate()
        {
            _moveSystem.Update();
        }
        public void Exit()
        {
        }
    }
}