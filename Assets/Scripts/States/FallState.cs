using Controllers;
using Systems;
using UnityEngine;
using DG.Tweening;

namespace States
{
    public class FallState : IState
    {
        private PlayerController player;
        private MoveSystem _moveSystem;
        private MoveComponent _moveComponent;
        private JumpComponent _jumpComponent;
        private AnimationComponentsComposer _animationComponent;
        private ColorPositioningComponent _colorPositioningComponent;

        private Transform child;
        private float targetZ;

        public FallState(PlayerController player) => this.player = player;

        public void Enter()
        {
            _moveSystem = player.GetControllerSystem<MoveSystem>();
            _jumpComponent = player.GetControllerComponent<JumpComponent>();
            _moveComponent = player.GetControllerComponent<MoveComponent>();
            _animationComponent = player.GetControllerComponent<AnimationComponentsComposer>();
            _colorPositioningComponent = player.GetControllerComponent<ColorPositioningComponent>();

            child = player.transform.GetChild(0);
        }

        public void FixedUpdate()
        {
            if (_animationComponent.CurrentState != "FallDown")
            {
                _animationComponent.CrossFadeState("FallDown", 0.1f);
            }

            player.baseFields.rb.gravityScale =
                _jumpComponent.gravityScale * _jumpComponent.fallGravityMultiplier;

            _moveSystem.Update();

            float newTargetZ = -3 * -_moveComponent.direction.x;

            if (!Mathf.Approximately(newTargetZ, targetZ))
            {
                targetZ = newTargetZ;

                child.DOKill();
                child.DORotate(
                    new Vector3(0, 0, targetZ),
                    0.2f
                ).SetEase(Ease.OutSine);
            }
        }

        public void Exit()
        {
            child.DOKill();
            child.DORotate(Vector3.zero, 0.2f).SetEase(Ease.OutSine);

            player.baseFields.rb.gravityScale = _jumpComponent.gravityScale;
        }
    }
}
