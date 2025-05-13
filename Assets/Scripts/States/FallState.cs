using Controllers;
using Systems;
using UnityEngine;

namespace States
{
    public class FallState : IState
    {
        private PlayerController player;
        private MoveSystem _moveSystem;
        private MoveComponent _moveComponent;
        private JumpComponent _jumpComponent;
        float rotation = 0;
        public FallState(PlayerController player) => this.player = player;
        public void Enter()
        {
            player.animator.CrossFade("FallDown",0.1f);
            _moveSystem = player.GetControllerSystem<MoveSystem>();
            _jumpComponent = player.GetControllerComponent<JumpComponent>();
            _moveComponent = player.GetControllerComponent<MoveComponent>();
        }
        public void Update()
        {
            player.baseFields.rb.gravityScale = _jumpComponent.gravityScale * _jumpComponent.fallGravityMultiplier;
            _moveSystem.OnUpdate();
        }
        public void Exit()
        {
            player.baseFields.rb.gravityScale = _jumpComponent.gravityScale;
        }
        
    }
}