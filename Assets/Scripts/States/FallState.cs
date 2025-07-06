using Controllers;
using Systems;
using UnityEngine;

namespace States
{
    public class FallState : IState
    {
        private PlayerController player;
        private MoveSystem _moveSystem;
        private MoveComponent _moveComponent;
        private JumpComponent _jumpComponent;
        private AnimationComponent _animationComponent;
        private ColorPositioningComponent _colorPositioningComponent;
        public FallState(PlayerController player) => this.player = player;
        public void Enter()
        {
            _moveSystem = player.GetControllerSystem<MoveSystem>();
            _jumpComponent = player.GetControllerComponent<JumpComponent>();
            _moveComponent = player.GetControllerComponent<MoveComponent>();
            _animationComponent = player.GetControllerComponent<AnimationComponent>();
            _colorPositioningComponent = player.GetControllerComponent<ColorPositioningComponent>();
        }
        public void FixedUpdate()
        {
            if (_animationComponent.currentState != "FallDown")
            {
                _animationComponent.CrossFade("FallDown",0.1f);
            }
            player.baseFields.rb.gravityScale = _jumpComponent.gravityScale * _jumpComponent.fallGravityMultiplier;
            _moveSystem.Update();
            var rot = _colorPositioningComponent.spriteRenderer.transform.eulerAngles;
            rot.z = Mathf.MoveTowardsAngle(rot.z, -3 * -_moveComponent.direction.x, 0.1f);
            _colorPositioningComponent.spriteRenderer.transform.rotation = Quaternion.Euler(rot);
        }
        public void Exit()
        {
            _colorPositioningComponent.spriteRenderer.transform.rotation = Quaternion.Euler(Vector3.zero);
            player.baseFields.rb.gravityScale = _jumpComponent.gravityScale;
        }
        
    }
}