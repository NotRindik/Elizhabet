using Controllers;
using Systems;
using UnityEngine;

namespace States
{
    public class IdleState : IState
    {
        private PlayerController player;

        public IdleState(PlayerController player) => this.player = player;

        private FrictionSystem _frictionSystem;

        public void Enter()
        {
            _frictionSystem = player.GetControllerSystem<FrictionSystem>();
        }

        public void Update()
        {
            _frictionSystem.Update();
        }

        public void Exit() { }
    }
}