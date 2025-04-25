using Controllers;
using Systems;
using UnityEngine;

namespace States
{
    public class JumpState : IState
    {
        private PlayerController player;
        private JumpSystem _jumpSystem;
        private MoveSystem _moveSystem;
        public JumpState(PlayerController player) => this.player = player;
        public void Enter()
        {
            _jumpSystem = player.GetControllerSystem<JumpSystem>();
            _moveSystem = player.GetControllerSystem<MoveSystem>();
            _jumpSystem.TryJump();
        }
        public void Update()
        {
            _moveSystem.Update();
        }
        public void Exit()
        {
        }
    }
    public class JumpUpState : IState
    {
        private PlayerController player;
        private JumpSystem _jumpSystem;
        private MoveSystem _moveSystem;
        public JumpUpState(PlayerController player) => this.player = player;
        public void Enter()
        {
            _jumpSystem = player.GetControllerSystem<JumpSystem>();
            _moveSystem = player.GetControllerSystem<MoveSystem>();
            _jumpSystem.OnJumpUp();
        }
        public void Update()
        {
            _moveSystem.Update();
        }
        public void Exit()
        {
        }
    }
}