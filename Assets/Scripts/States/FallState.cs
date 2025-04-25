using Controllers;
using Systems;

namespace States
{
    public class FallState : IState
    {
        private PlayerController player;
        private MoveSystem _moveSystem;
        private JumpComponent _jumpComponent;
        public FallState(PlayerController player) => this.player = player;
        public void Enter()
        {
            _moveSystem = player.GetControllerSystem<MoveSystem>();
            _jumpComponent = player.GetControllerComponent<JumpComponent>();
        }
        public void Update()
        {
            player.baseFields.rb.gravityScale = _jumpComponent.gravityScale * _jumpComponent.fallGravityMultiplier;
            _moveSystem.Update();
        }
        public void Exit()
        {
            player.baseFields.rb.gravityScale = _jumpComponent.gravityScale;
        }
        
    }
}