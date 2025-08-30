using Controllers;
using Systems;
using UnityEngine;

namespace States
{
    public class WalkState : IState
    {
        private EntityController player;
        private MoveComponent _moveComponent;
        private MoveSystem _moveSystem;

        private AnimationComponentsComposer _animationComponent;


        public WalkState(EntityController player) => this.player = player;

        public void Enter()
        {
            _animationComponent = player.GetControllerComponent<AnimationComponentsComposer>();
            _moveComponent = player.GetControllerComponent<MoveComponent>();
            _moveSystem = player.GetControllerSystem<MoveSystem>();
        }

        public void FixedUpdate()
        {
            _animationComponent.CrossFadeState("Walk", 0.1f);
            _moveSystem.Update();
        }
            
        public void Exit() { }
    }
}