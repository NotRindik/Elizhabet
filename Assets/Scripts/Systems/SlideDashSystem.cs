using System.Collections;
using Assets.Scripts;
using Controllers;
using States;
using UnityEngine;

namespace Systems
{
    public class SlideDashSystem : BaseSystem
    {
        private DashComponent _dashComponent;
        private MoveSystem _moveSystem;
        private SlideComponent _slideComponent;
        private AnimationComponentsComposer animationComponent;
        private WallEdgeClimbComponent wallEdgeClimbComponent;
        private EntityController entity;
        private GroundingComponent _groundingComponent;
        private FSMSystem _fsm;
        private RendererCollection _playerCustomize;
        private GravityScalerSystem _gravityScalerSystem;
        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _dashComponent = owner.GetControllerComponent<DashComponent>();
            animationComponent = owner.GetControllerComponent<AnimationComponentsComposer>();
            _slideComponent = owner.GetControllerComponent<SlideComponent>();
            _groundingComponent = owner.GetControllerComponent<GroundingComponent>();
            _playerCustomize = owner.GetControllerComponent<RendererCollection>();
            wallEdgeClimbComponent = owner.GetControllerComponent<WallEdgeClimbComponent>();
            _gravityScalerSystem = owner.GetControllerSystem<GravityScalerSystem>();
            _moveSystem = owner.GetControllerSystem<MoveSystem>();
            _fsm = owner.GetControllerSystem<FSMSystem>();
            owner.OnUpdate += Timers;
            entity = (EntityController)owner;
        }

        public void Timers()
        {
            if (_slideComponent.SlideProcess == null && (_groundingComponent.isGround || wallEdgeClimbComponent.EdgeStuckProcess != null) && _dashComponent.DashProcess == null)
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
            if (_dashComponent.DashProcess == null && wallEdgeClimbComponent.EdgeStuckProcess == null && _dashComponent.allowDash)
            {
                _dashComponent.allowDash = false;
                AudioManager.instance.PlaySoundEffect($"{FileManager.SFX}Dash");
                _dashComponent.DashProcess = mono.StartCoroutine(DashProcess());
            }
        }

        private IEnumerator DashProcess()
        {
            Rigidbody2D rb = entity.baseFields.rb;
            Vector2 slideVelocityTemp = rb.linearVelocity;
            float dashDistance = _dashComponent.dashDistance;
            float dashDuration = _dashComponent.dashDuration;
            float dashDirection = Mathf.Sign(transform.localScale.x);
            _moveSystem.IsActive = false;
            Vector2 startPos = rb.position;
            Vector2 targetPos = startPos + Vector2.right * dashDirection * dashDistance;

            float residualSpeed = rb.linearVelocityX;
            float elapsed = 0f;
            animationComponent.CrossFadeState("Slide",0.1f);
            _dashComponent.ghostTrail.StartTrail();
            _dashComponent.isDash = true;
            _gravityScalerSystem.IsActive = false ;
            while (elapsed < dashDuration)
            {
                rb.gravityScale = 0;
                rb.linearVelocityY = 0f;
                float t = elapsed / dashDuration;
                rb.MovePosition(Vector2.Lerp(startPos, targetPos, t));
                _playerCustomize.renderers["Hair"].color = Color32.Lerp(new Color32(255,255,255,255),new Color32(0, 183, 255, 255),t);
                if (wallEdgeClimbComponent.EdgeStuckProcess != null)
                {
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
            rb.gravityScale = 1;
            _gravityScalerSystem.IsActive = true;
            _moveSystem.IsActive = true;
            _dashComponent.ghostTrail.StopTrail();
            _playerCustomize.renderers["Hair"].color = new Color32(255, 255, 255, 255);
            _dashComponent.isDash = false;
            _dashComponent.DashProcess = null;
            _fsm.SetState(new SlideState((PlayerController)owner));
            rb.linearVelocityX = dashDirection * Mathf.Abs(residualSpeed * _dashComponent.dashSlideForce);
        }
    }
}