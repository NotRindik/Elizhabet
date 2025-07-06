using Controllers;
using Systems;
using UnityEngine;

namespace States
{
    public class WalkState : IState
    {
        private PlayerController player;
        private MoveComponent _moveComponent;
        private MoveSystem _moveSystem;
        private AnimationComponent animationComponent;
        public WalkState(PlayerController player) => this.player = player;

        public void Enter()
        {
            animationComponent = player.GetControllerComponent<AnimationComponent>();
            animationComponent.CrossFade("Walk",0.1f);
            _moveComponent = player.GetControllerComponent<MoveComponent>();
            _moveSystem = player.GetControllerSystem<MoveSystem>();
        }

        public void FixedUpdate()
        {
            if(animationComponent.currentState != "Walk")
                animationComponent.CrossFade("Walk",0.1f);
            _moveSystem.Update();
        }

        public void Exit() { }
    }
}