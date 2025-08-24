using System.Collections;
using Assets.Scripts;
using Controllers;
using Systems;
using UnityEngine;
using UnityEngine.Serialization;

namespace Systems
{
    public class SlideSystem : BaseSystem,IStopCoroutineSafely
    {
        private AnimationComponentsComposer _animationComponent;


        private SlideComponent _slideComponent;
        private SpriteFlipSystem _flipSystem;
        private IInputProvider _inputProvider;
        private Rigidbody2D _rb;
        
        private ColorPositioningComponent _colorPositioning;

        private Transform spriteTransform;
        private Vector3 originalScale;

        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _animationComponent = owner.GetControllerComponent<AnimationComponentsComposer>();
            _slideComponent = owner.GetControllerComponent<SlideComponent>();
            _flipSystem = owner.GetControllerSystem<SpriteFlipSystem>();
            _colorPositioning = owner.GetControllerComponent<ColorPositioningComponent>();
            _inputProvider = owner.GetControllerSystem<IInputProvider>();
            _rb = owner.GetControllerComponent<ControllersBaseFields>().rb;

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
            _animationComponent.CrossFadeState("Slide", 0.1f);
            _flipSystem.IsActive = false;

            spriteTransform = _colorPositioning.spriteRenderer.transform;
            originalScale = spriteTransform.localScale;

            float pulseTimer = 0f;
            float pulseSpeed = 8f; // Чем выше — тем быстрее "пульсация"
            float pulseAmplitude = 0.04f; // Амплитуда: от 1 ± 0.2 => [0.8..1.2]

            while (true)
            {

                float velX = Mathf.Abs(_rb.linearVelocityX);
                _slideComponent.isCeilOpen = !Physics2D.BoxCast(_colorPositioning.pointsGroup[ColorPosNameConst.HEAD].FirstActivePoint(), _slideComponent.boxCastSize, 0, Vector3.up,_slideComponent.ceilCheckDist,_slideComponent.LayerMask);
                if ((_inputProvider.GetState().Jump.IsPressed || Mathf.Abs(_rb.linearVelocityY) > 10) && _slideComponent.isCeilOpen)
                    break;
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
            Gizmos.DrawWireCube(_colorPositioning.pointsGroup[ColorPosNameConst.HEAD].FirstActivePoint(), _slideComponent.boxCastSize);
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
                    if (Mathf.Abs(_rb.linearVelocityX) < 0.2f)
                        _rb.AddForce(owner.transform.right * owner.transform.localScale.x * _slideComponent.onStuckImpuleForce, ForceMode2D.Impulse);
                }
            }

            _slideComponent.isSlide = _slideComponent.SlideProcess != null;
        }

        public void StopCoroutineSafely()
        {
            if (_slideComponent.SlideProcess != null)
            {
                owner.StopCoroutine(_slideComponent.SlideProcess);
                _slideComponent.isCeilOpen = true;
                spriteTransform.localScale = originalScale;
                _flipSystem.IsActive = true;
                _slideComponent.SlideProcess = null;
            }
        }
    }
    
    [System.Serializable]
    public class SlideComponent : IComponent
    {
        public Coroutine SlideProcess;
        public bool isSlide;
        public float force;
        public float onStuckImpuleForce = 10;
        public float frictionCoefficient;
        public float ceilCheckDist;
        public float velocityIfCeil;
        public LayerMask LayerMask;
        public bool isCeilOpen;
        public Vector2 boxCastSize;
    }
}