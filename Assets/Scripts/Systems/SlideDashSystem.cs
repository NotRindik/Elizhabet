using System.Collections;
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
        private AnimationComponent animationComponent;
        private WallEdgeClimbComponent wallEdgeClimbComponent;
        private EntityController entity;
        private GroundingComponent _groundingComponent;
        private FSMSystem _fsm;
        private PlayerCustomizer _playerCustomize;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _dashComponent = owner.GetControllerComponent<DashComponent>();
            animationComponent = owner.GetControllerComponent<AnimationComponent>();
            _slideComponent = owner.GetControllerComponent<SlideComponent>();
            _groundingComponent = owner.GetControllerComponent<GroundingComponent>();
            _playerCustomize = owner.GetControllerComponent<PlayerCustomizer>();
            wallEdgeClimbComponent = owner.GetControllerComponent<WallEdgeClimbComponent>();
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
                _dashComponent.DashProcess = owner.StartCoroutine(DashProcess());
            }
        }

        private IEnumerator DashProcess()
        {
            Rigidbody2D rb = entity.baseFields.rb;
            Vector2 slideVelocityTemp = rb.linearVelocity;
            float dashDistance = _dashComponent.dashDistance;
            float dashDuration = _dashComponent.dashDuration;
            float dashDirection = Mathf.Sign(owner.transform.localScale.x);
            _moveSystem.IsActive = false;
            Vector2 startPos = rb.position;
            Vector2 targetPos = startPos + Vector2.right * dashDirection * dashDistance;

            float residualSpeed = rb.linearVelocityX;
            float elapsed = 0f;
            animationComponent.CrossFade("Slide",0.1f);
            _dashComponent.ghostTrail.StartTrail();
            _dashComponent.isDash = true;
            while (elapsed < dashDuration)
            {
                float t = elapsed / dashDuration;
                rb.MovePosition(Vector2.Lerp(startPos, targetPos, t));
                _playerCustomize.hairSprire.color = Color32.Lerp(new Color32(255,255,255,255),new Color32(0, 183, 255, 255),t);
                if (wallEdgeClimbComponent.EdgeStuckProcess != null)
                {
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
            
            _moveSystem.IsActive = true;
            _dashComponent.ghostTrail.StopTrail();
            _playerCustomize.hairSprire.color = new Color32(255, 255, 255, 255);
            _dashComponent.isDash = false;
            _dashComponent.DashProcess = null;
            _fsm.SetState(new SlideState((PlayerController)owner));
            rb.linearVelocityX = dashDirection * Mathf.Abs(residualSpeed * _dashComponent.dashSlideForce);
        }
    }
}