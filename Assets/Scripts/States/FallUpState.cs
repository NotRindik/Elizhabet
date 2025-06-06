using Controllers;
using Systems;
using UnityEngine;

namespace States
{
    public class FallUpState : IState
    {
        private PlayerController player;
        private MoveSystem _moveSystem;
        private MoveComponent _moveComponent;
        private JumpComponent _jumpComponent;
        private AnimationComponent _animationComponent;
        private ColorPositioningComponent _colorPositioningComponent;
        public FallUpState(PlayerController player) => this.player = player;
        public void Enter()
        {
            _moveSystem = player.GetControllerSystem<MoveSystem>();
            _jumpComponent = player.GetControllerComponent<JumpComponent>();
            _moveComponent = player.GetControllerComponent<MoveComponent>();
            _animationComponent = player.GetControllerComponent<AnimationComponent>();
            _colorPositioningComponent = player.GetControllerComponent<ColorPositioningComponent>();
        }
        public void Update()
        {
            if (_animationComponent.currentState != "FallUp")
            {
                _animationComponent.CrossFade("FallUp",0.1f);
            }
            _moveSystem.Update();
            var rot = _colorPositioningComponent.spriteRenderer.transform.eulerAngles;
            rot.z = Mathf.MoveTowardsAngle(rot.z, 8 * -_moveComponent.direction.x, 0.1f);
            _colorPositioningComponent.spriteRenderer.transform.rotation = Quaternion.Euler(rot);
        }
        public void Exit()
        {
            player.transform.rotation = UnityEngine.Quaternion.Euler(0,0,0);
        }
    }
}