using Controllers;
using Systems;
using UnityEngine;
using DG.Tweening;

namespace States
{
    public class FallUpState : IState
    {
        private PlayerController player;
        private MoveSystem _moveSystem;
        private MoveComponent _moveComponent;
        private JumpComponent _jumpComponent;

        private AnimationComponentsComposer _animationComponent;

        private float targetZ;
        private Transform child;

        public FallUpState(PlayerController player) => this.player = player;
        public void Enter()
        {
            _moveSystem = player.GetControllerSystem<MoveSystem>();
            _jumpComponent = player.GetControllerComponent<JumpComponent>();
            _moveComponent = player.GetControllerComponent<MoveComponent>();
            _animationComponent = player.GetControllerComponent<AnimationComponentsComposer>();

            child = player.transform.GetChild(0);
        }

        public void FixedUpdate()
        {
            if (_animationComponent.CurrentState != "FallUp")
            {
                _animationComponent.CrossFadeState("FallUp", 0.1f);
            }

            _moveSystem.Update();

            float newTargetZ = 8 * -_moveComponent.direction.x;

            // Только если цель реально поменялась
            if (!Mathf.Approximately(newTargetZ, targetZ))
            {
                targetZ = newTargetZ;

                child.DOKill(); // убиваем старый твин
                child.DORotate(
                    new Vector3(0, 0, targetZ),
                    0.2f
                ).SetEase(Ease.OutSine);
            }
        }

        public void Exit()
        {
            child.DOKill(); // чтобы твин не жил после выхода
            player.baseFields.rb.gravityScale = _jumpComponent.gravityScale;

            // вернём в исходное положение (0 по Z)
            child.DORotate(Vector3.zero, 0.2f).SetEase(Ease.OutSine);
        }

    }
}