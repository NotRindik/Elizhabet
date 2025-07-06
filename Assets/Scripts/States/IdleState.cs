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
        private AnimationComponent _animationComponent;
        
        public void Enter()
        {
            _moveSystem = player.GetControllerSystem<MoveSystem>();
            _frictionSystem = player.GetControllerSystem<FrictionSystem>();
            _animationComponent = player.GetControllerComponent<AnimationComponent>();
        }

        public void FixedUpdate()
        {
            if (_animationComponent.currentState != "Idle")
            {
                _animationComponent.CrossFade("Idle",0.1f);
            }
            _moveSystem.Update();
        }

        public void Exit()
        {
        }
    }
}