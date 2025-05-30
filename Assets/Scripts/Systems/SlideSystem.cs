using System.Collections;
using Assets.Scripts;
using Controllers;
using Systems;
using UnityEngine;
using UnityEngine.Serialization;

namespace Systems
{
    public class SlideSystem : BaseSystem
    {
        private AnimationComponent _animatorState;
        private SlideComponent _slideComponent;
        private SpriteFlipSystem _flipSystem;
        private JumpComponent _jumpComponent;
        private Rigidbody2D _rb;
        
        private ColorPositioningComponent _colorPositioning;
        

        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _animatorState = owner.GetControllerComponent<AnimationComponent>();
            _slideComponent = owner.GetControllerComponent<SlideComponent>();
            _flipSystem = owner.GetControllerSystem<SpriteFlipSystem>();
            _colorPositioning = owner.GetControllerComponent<ColorPositioningComponent>();
            _jumpComponent = owner.GetControllerComponent<JumpComponent>();
            _rb = ((EntityController)owner).baseFields.rb;

            owner.OnFixedUpdate += FixedUpdate;
            owner.OnGizmosUpdate += OnDrawGizmos;
        }
        public override void OnUpdate()
        {
            base.OnUpdate();
            if (_slideComponent.SlideProcess == null)
            {
                _slideComponent.SlideProcess = owner.StartCoroutine(SlideProcess());
            }
        }

        private IEnumerator SlideProcess()
        {
            _animatorState.CrossFade("Slide", 0.1f);
            _flipSystem.IsActive = false;

            Transform spriteTransform = _colorPositioning.spriteRenderer.transform;
            Vector3 originalScale = spriteTransform.localScale;

            float pulseTimer = 0f;
            float pulseSpeed = 8f; // Чем выше — тем быстрее "пульсация"
            float pulseAmplitude = 0.04f; // Амплитуда: от 1 ± 0.2 => [0.8..1.2]

            while (true)
            {
                if (!_jumpComponent.isGround)
                    break;

                float velX = Mathf.Abs(_rb.linearVelocityX);
                _slideComponent.isCeilOpen = !Physics2D.Raycast(_colorPositioning.pointsGroup[ColorPosNameConst.HEAD].FirstActivePoint(), Vector3.up, _slideComponent.ceilCheckDist, _slideComponent.LayerMask);
                if (velX < 0.1f)
                {
                    spriteTransform.localScale = originalScale;
                    yield return null;
                    continue;
                }
                
                pulseTimer += Time.deltaTime * pulseSpeed * velX;
                float scaleX = 1f + Mathf.Sin(pulseTimer) * pulseAmplitude;

                spriteTransform.localScale = new Vector3(scaleX, 1f, 1f);

                yield return null;
            }
            _slideComponent.isCeilOpen = true;
            spriteTransform.localScale = originalScale;
            _flipSystem.IsActive = true;
            _slideComponent.SlideProcess = null;
        }

        public void OnDrawGizmos()
        {
            Gizmos.DrawRay(_colorPositioning.pointsGroup[ColorPosNameConst.HEAD].FirstActivePoint(), Vector3.up * _slideComponent.ceilCheckDist);
        }


        public void FixedUpdate()
        {
            if (_slideComponent.SlideProcess != null)
            {
                if(_slideComponent.isCeilOpen)
                    _rb.linearVelocityX = Mathf.MoveTowards(_rb.linearVelocityX, 0, _slideComponent.frictionCoefficient);
                else
                {
                    _rb.linearVelocityX = Mathf.MoveTowards(_rb.linearVelocityX, _slideComponent.velocityIfCeil * owner.transform.localScale.x, _slideComponent.frictionCoefficient);
                }
            }
        }
        
    }
    
    [System.Serializable]
    public class SlideComponent : IComponent
    {
        public Coroutine SlideProcess;
        public float force;
        public float frictionCoefficient;
        public float ceilCheckDist;
        public float velocityIfCeil;
        public LayerMask LayerMask;
        public bool isCeilOpen;
    }
}