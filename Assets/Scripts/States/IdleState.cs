using Controllers;
using Systems;
using UnityEngine;

namespace States
{
    public class IdleState : IState
    {
        private EntityController player;

        public IdleState(EntityController player) => this.player = player;
        private MoveSystem _moveSystem;
        private FrictionSystem _frictionSystem;
        
        public void Enter()
        {
            _moveSystem = player.GetControllerSystem<MoveSystem>();
            _frictionSystem = player.GetControllerSystem<FrictionSystem>();
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