using Controllers;
using Systems;

namespace States
{
    public class AttackState:IState
    {
        private Controller _controller;
        private AttackSystem _attackSystem;
        private SpriteFlipSystem _spriteFlipSystem;
        private ControllersBaseFields _baseFields;
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
            _spriteFlipSystem.IsActive = false;
        }

        public void Enter()
        {
            speedTemp = _moveComponent.speed;
            _moveComponent.speed = 1;
            _attackSystem.Update();
        }
        public void Update()
        {
            if(_groundingComponent.isGround)
                _baseFields.rb.linearVelocityX = 0;
            _moveSystem.Update();
        }
        public void Exit()
        {
            _moveComponent.speed = speedTemp;
            _spriteFlipSystem.IsActive = true;
        }
    }
}