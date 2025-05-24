using Controllers;
using Systems;
using UnityEngine;

namespace States
{
    public class JumpState : IState
    {
        private PlayerController player;
        private JumpSystem _jumpSystem;
        private MoveSystem _moveSystem;
        private MoveComponent _moveComponent;
        private WallEdgeClimbComponent _wallEdgeClimbComponent;
        private ColorPositioningComponent _colorPositioning;
        private AnimationComponent _animationComponent;
        private SlideComponent _slideComponent;
        public JumpState(PlayerController player) => this.player = player;
        public void Enter()
        {
            _jumpSystem = player.GetControllerSystem<JumpSystem>();
            _moveSystem = player.GetControllerSystem<MoveSystem>();
            _wallEdgeClimbComponent = player.GetControllerComponent<WallEdgeClimbComponent>();
            _moveComponent = player.GetControllerComponent<MoveComponent>();
            _colorPositioning = player.GetControllerComponent<ColorPositioningComponent>();
            _animationComponent = player.GetControllerComponent<AnimationComponent>();
            _slideComponent = player.GetControllerComponent<SlideComponent>();

            if (_wallEdgeClimbComponent != null)
            {
                if (_wallEdgeClimbComponent.EdgeStuckProcess != null)
                {
                    OnWalledgeClimb();
                }
            }
            
            var isJump = _jumpSystem.TryJump();
            if(isJump)
                _animationComponent.CrossFade("FallUp",0.1f);
        }
        private void OnWalledgeClimb()
        {

            player.baseFields.rb.bodyType = RigidbodyType2D.Dynamic;
            player.baseFields.rb.gravityScale = 1;
            player.StopCoroutine(_wallEdgeClimbComponent.EdgeStuckProcess);
            _wallEdgeClimbComponent.EdgeStuckProcess = null;
            _wallEdgeClimbComponent.Reset();
            _jumpSystem.Jump();
            _animationComponent.CrossFade("FallUp",0.1f);
        }
        
        public void Update()
        {
            _moveSystem.Update();
            var rot = _colorPositioning._spriteRenderer.transform.eulerAngles;
            rot.z = Mathf.MoveTowardsAngle(rot.z, 8 * -_moveComponent.direction.x, 0.1f);
            _colorPositioning._spriteRenderer.transform.rotation = Quaternion.Euler(rot);
        }
        public void Exit()
        {
            player.transform.rotation = UnityEngine.Quaternion.Euler(0,0,0);
        }
    }
    public class JumpUpState : IState
    {
        private PlayerController player;
        private JumpSystem _jumpSystem;
        private MoveSystem _moveSystem;
        public JumpUpState(PlayerController player) => this.player = player;
        public void Enter()
        {
            _jumpSystem = player.GetControllerSystem<JumpSystem>();
            _moveSystem = player.GetControllerSystem<MoveSystem>();
            _jumpSystem.OnJumpUp();
        }
        public void Update()
        {
            _moveSystem.OnUpdate();
        }
        public void Exit()
        {
        }
    }
}