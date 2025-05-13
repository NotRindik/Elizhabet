using Controllers;
using Systems;
using UnityEngine;

namespace States
{
    public class IdleState : IState
    {
        private PlayerController player;

        public IdleState(PlayerController player) => this.player = player;
        private MoveSystem _moveSystem;
        private FrictionSystem _frictionSystem;

        public void Enter()
        {
            _moveSystem = player.GetControllerSystem<MoveSystem>();
            player.animator.CrossFade("Idle",0.1f);
            _frictionSystem = player.GetControllerSystem<FrictionSystem>();
        }

        public void Update()
        {
            _moveSystem.OnUpdate();
            _frictionSystem.OnUpdate();
        }

        public void Exit()
        {
        }
    }
}