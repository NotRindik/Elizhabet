using Controllers;
using Systems;
using UnityEngine;

namespace States
{
    public class WalkState : IState
    {
        private PlayerController player;
        private MoveComponent _moveComponent;
        private AnimationComponent animationComponent;
        public WalkState(PlayerController player) => this.player = player;

        public void Enter()
        {
            animationComponent = player.GetControllerComponent<AnimationComponent>();
            animationComponent.CrossFade("Walk",0.1f);
            _moveComponent = player.GetControllerComponent<MoveComponent>();
        }

        public void Update()
        {
            if(animationComponent.currentState != "Walk")
                animationComponent.CrossFade("Walk",0.1f);
            player.GetControllerSystem<MoveSystem>().Update();
        }

        public void Exit() { }
    }
}