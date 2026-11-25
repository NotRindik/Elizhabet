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
        private ColorPositioningComponent _colorPositioningComponent;
        private Tween _rotationTween;
        private Transform child;
        private float targetZ;

        public FallState(PlayerController player) => this.player = player;

        public void Enter()
        {
            _moveSystem = player.GetControllerSystem<MoveSystem>();
            _jumpComponent = player.GetControllerComponent<JumpComponent>();
            _moveComponent = player.GetControllerComponent<MoveComponent>();
            _colorPositioningComponent = player.GetControllerComponent<ColorPositioningComponent>();

            child = player.transform.GetChild(0);
        }

        public void FixedUpdate()
        {
            _moveSystem.Update();

            float target = Mathf.Approximately(_moveComponent.direction.x, 0f)
                ? 0f
                : 15f;

            if (!Mathf.Approximately(target, targetZ) && _rotationTween == null)
            {
                targetZ = target;

                _rotationTween?.Kill();

                _rotationTween = child.DOLocalRotate(
                    new Vector3(0, 0, targetZ),
                    0.2f
                ).SetEase(Ease.OutSine);
            }
        }

        public void Exit()
        {
            _rotationTween?.Kill();
            _rotationTween = null;

            targetZ = 0f;

            player.baseFields.rb.gravityScale = _jumpComponent.gravityScale;

            child
                .DOLocalRotate(Vector3.zero, 0.2f)
                .SetEase(Ease.OutSine);
        }
    }
}