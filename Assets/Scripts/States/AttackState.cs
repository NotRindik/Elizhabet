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
        public AttackState(Controller controller)
        {
            _controller = controller;
            _attackSystem = controller.GetControllerSystem<AttackSystem>();
            _spriteFlipSystem = controller.GetControllerSystem<SpriteFlipSystem>();
            _baseFields = controller.GetControllerComponent<ControllersBaseFields>();
            _groundingComponent = controller.GetControllerComponent<GroundingComponent>();
            _spriteFlipSystem.IsActive = false;
        }

        public void Enter()
        {
            _attackSystem.Update();
        }
        public void Update()
        {
            if(_groundingComponent.isGround)
                _baseFields.rb.linearVelocityX = 0;
        }
        public void Exit()
        {
            _spriteFlipSystem.IsActive = true;
        }
    }
}