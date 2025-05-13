using Controllers;
using Systems;
using UnityEngine;

namespace States
{
    public class WalkState : IState
    {
        private PlayerController player;
        private MoveComponent _moveComponent;
        public WalkState(PlayerController player) => this.player = player;

        public void Enter()
        {
            player.animator.CrossFade("Walk",0.1f);
            _moveComponent = player.GetControllerComponent<MoveComponent>();
        }

        public void Update()
        {
            player.GetControllerSystem<MoveSystem>().OnUpdate();
        }

        public void Exit() { }
    }
}