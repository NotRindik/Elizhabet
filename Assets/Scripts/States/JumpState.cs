using Assets.Scripts;
using Controllers;
using Systems;
using UnityEngine;

namespace States
{
    public class JumpState : IState
    {
        private EntityController player;
        private JumpSystem _jumpSystem;
        private MoveSystem _moveSystem;
        public JumpState(EntityController player) => this.player = player;
        public void Enter()
        {
            _jumpSystem = player.GetControllerSystem<JumpSystem>();
            _moveSystem = player.GetControllerSystem<MoveSystem>();

            AudioManager.instance.PlaySoundEffect($"{FileManager.SFX}Jump");
            
            _jumpSystem.Jump();
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
        private EntityController player;
        private JumpSystem _jumpSystem;
        private MoveSystem _moveSystem;
        public JumpUpState(EntityController player) => this.player = player;
        public void Enter()
        {
            _jumpSystem = player.GetControllerSystem<JumpSystem>();
            _moveSystem = player.GetControllerSystem<MoveSystem>();
            _jumpSystem.OnJumpUp();
        }
        public void FixedUpdate()
        {
            _moveSystem.OnUpdate();
        }
        public void Exit()
        {
        }
    }
}