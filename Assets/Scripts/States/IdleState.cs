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
        private AnimationComponentsComposer _animationComponent;
        
        public void Enter()
        {
            _moveSystem = player.GetControllerSystem<MoveSystem>();
            _frictionSystem = player.GetControllerSystem<FrictionSystem>();
            _animationComponent = player.GetControllerComponent<AnimationComponentsComposer>();
        }

        public void FixedUpdate()
        {
            if (_animationComponent.CurrentState != "Idle")
            {
                _animationComponent.CrossFadeState("Idle", 0.1f);
            }
            _moveSystem.Update();
        }

        public void Exit()
        {
        }
    }
}