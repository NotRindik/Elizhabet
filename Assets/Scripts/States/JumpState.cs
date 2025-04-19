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
        }
        public void Update()
        {
            _jumpSystem.Jump();
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
        }
        public void Update()
        {
            _moveSystem.Update();
            _jumpSystem.OnJumpUp();
        }
        public void Exit()
        {
        }
    }
}