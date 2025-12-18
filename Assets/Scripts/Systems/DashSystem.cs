using System.Collections;
using Assets.Scripts;
using Controllers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Systems
{
    public class DashSystem : BaseSystem
    {

        private DashComponent _dashComponent;
        private MoveComponent _moveComponent;
        private GroundingComponent _groundingComponent;
        private SlideComponent _slideComponent;
        private AnimationComponentsComposer _animationComponent;
        private WallEdgeClimbComponent wallEdgeClimbComponent;
        private EntityController entity;
        private RendererCollection _playerCustomize;
        public override void Initialize(IController owner)
        {
            base.Initialize(owner);
            _dashComponent = owner.GetControllerComponent<DashComponent>();
            _moveComponent = owner.GetControllerComponent<MoveComponent>();
            _groundingComponent = owner.GetControllerComponent<GroundingComponent>();
            _playerCustomize = owner.GetControllerComponent<RendererCollection>();
            _slideComponent = owner.GetControllerComponent<SlideComponent>();
            _animationComponent = owner.GetControllerComponent<AnimationComponentsComposer>();
            wallEdgeClimbComponent = owner.GetControllerComponent<WallEdgeClimbComponent>();
            owner.OnUpdate += Timers;
            entity = (EntityController)owner;
        }

        public void Timers()
        {
            if ((_groundingComponent.isGround || wallEdgeClimbComponent.EdgeStuckProcess != null) && _slideComponent.SlideProcess == null )
            {
                _dashComponent.allowDash = true;
            }
            
        }

        public void OnDash()
        {
            Update();
        }
        
        public override void OnUpdate()
        {
            if (_dashComponent.DashProcess == null)
            {
                _dashComponent.allowDash = false;
                AudioManager.instance.PlaySoundEffect($"{FileManager.SFX}Dash");
                _dashComponent.DashProcess = mono.StartCoroutine(DashProcess());
            }
        }

        private IEnumerator DashProcess()
        {
            Rigidbody2D rb = entity.baseFields.rb;

            float dashDistance = _dashComponent.dashDistance;
            float dashDuration = _dashComponent.dashDuration;
            float dashDirection = Mathf.Sign(transform.localScale.x);

            Vector2 startPos = rb.position;
            Vector2 targetPos = startPos + Vector2.right * dashDirection * dashDistance;

            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;

            float elapsed = 0f;
            _animationComponent.CrossFadeState("FallUp",0.1f);
            _dashComponent.ghostTrail.StartTrail();
            _dashComponent.isDash = true;
            while (elapsed < dashDuration)
            {
                float t = elapsed / dashDuration;
                rb.MovePosition(Vector2.Lerp(startPos, targetPos, t));
                _playerCustomize.renderers["Hair"].color = Color32.Lerp(new Color32(255,255,255,255),new Color32(0, 183, 255, 255),t);
                if (wallEdgeClimbComponent.EdgeStuckProcess != null)
                {
                    break;
                }
                
                if (_dashComponent.allowCancel &&
                    Mathf.Sign(_moveComponent.direction.x) != dashDirection &&
                    _moveComponent.direction.x != 0)
                {
                    break;
                }

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
            rb.gravityScale = _dashComponent.defaultGravityScale;
            _dashComponent.ghostTrail.StopTrail();
            _playerCustomize.renderers["Hair"].color = new Color32(255,255,255,255);
            _dashComponent.isDash = false;
            yield return new WaitForSeconds(0.2f);
            _dashComponent.DashProcess = null;
        }
    }

    [System.Serializable]
    public class DashComponent : IComponent
    {
        public Coroutine DashProcess;
        public bool isDash;
        public bool allowDash;
        public float dashDuration = 0.15f;
        public bool allowCancel = true;
        public float dashDistance = 4f;
        public float dashSlideForce = 1.3f;
        public float defaultGravityScale = 1f;
        public SpriteGhostTrail ghostTrail;
    }
}