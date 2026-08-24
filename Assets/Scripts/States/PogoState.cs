using Controllers;
using DG.Tweening;
using Systems;
using UnityEngine;

namespace States
{
    [System.Serializable]
    public struct PivotsComponent : IComponent
    {
        public UnityEngine.Transform mainPivot;
    }

    public class PogoState : BasicState
    {
        private Tween _tween;
        private MoveSystem _moveSystem;
        private SpriteFlipSystem _spriteFlipSys;
        private SpriteFlipComponent _spriteFlipC;
        private AnimationComponentsComposer _animationComponentsComposer;
        private ControllersBaseFields _baseFields;
        private GroundingComponent _groundingComponent;
        private AttackComponent _attackComponent;
        private const float MinRotationSpeed = 360f;
        private const float MaxRotationSpeed = 720f;
        private const float VelocityForMaxRotation = 15f;
        private PivotsComponent _pivotsC;

        public PogoState(AbstractEntity entity) : base(entity)
        {
            _pivotsC = entity.GetControllerComponent<PivotsComponent>();
            _moveSystem = entity.GetControllerSystem<MoveSystem>();
            _spriteFlipSys = entity.GetControllerSystem<SpriteFlipSystem>();
            _attackComponent = entity.GetControllerComponent<AttackComponent>();
            _animationComponentsComposer = entity.GetControllerComponent<AnimationComponentsComposer>();
            _spriteFlipC = entity.GetControllerComponent<SpriteFlipComponent>();
            _groundingComponent = entity.GetControllerComponent<GroundingComponent>();
            _baseFields = entity.GetControllerComponent<ControllersBaseFields>();
        }

        public override void Enter()
        {
            Transform pivot = _pivotsC.mainPivot.transform;
            _spriteFlipSys.IsActive = false;
            _tween?.Kill();
            var rb = _baseFields.rb;
            rb.gravityScale = 0.8f;
            float direction = _spriteFlipC.IsFlip ? -1f : 1f;

            _tween = pivot
                .DOLocalRotate(
                    new Vector3(0f, 0f, 360f * direction),
                    360f / MinRotationSpeed,
                    RotateMode.FastBeyond360)
                .SetRelative()
                .SetLoops(-1, LoopType.Incremental)
                .SetEase(Ease.Linear);

            _animationComponentsComposer.PlayState("Locomotion", "Somersault");
        }

        public override void Update()
        {
            _moveSystem.Update();

            float velocity = _baseFields.rb.linearVelocity.magnitude;

            float t = Mathf.InverseLerp(
                0f,
                VelocityForMaxRotation,
                velocity);

            float rotationSpeed = Mathf.Lerp(
                MinRotationSpeed,
                MaxRotationSpeed,
                t);

            _tween.timeScale = rotationSpeed / MinRotationSpeed;
            
        }
        public override void Exit()
        {
            var rb = _baseFields.rb;
            rb.gravityScale = 1f;
            
            _tween?.Kill();
            _spriteFlipSys.IsActive = true;
            Transform pivot = _pivotsC.mainPivot.transform;
            if (_groundingComponent.IsReallyGrounded)
                _attackComponent.IsPogo = false;

            pivot.localRotation = Quaternion.Euler(Vector3.zero);
        }
    }


    public abstract class BasicState : IState
    {
        protected AbstractEntity entity;
        public BasicState(AbstractEntity entity)
        {
            this.entity = entity;
        }

        public virtual void Update()
        {

        }

        public abstract void Enter();

        public abstract void Exit();
    }
}
