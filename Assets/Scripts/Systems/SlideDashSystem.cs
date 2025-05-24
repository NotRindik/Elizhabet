using System.Collections;
using Controllers;
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
        private JumpComponent _jumpComponent;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _dashComponent = owner.GetControllerComponent<DashComponent>();
            animationComponent = owner.GetControllerComponent<AnimationComponent>();
            _slideComponent = owner.GetControllerComponent<SlideComponent>();
            _jumpComponent = owner.GetControllerComponent<JumpComponent>();
            wallEdgeClimbComponent = owner.GetControllerComponent<WallEdgeClimbComponent>();
            _moveSystem = owner.GetControllerSystem<MoveSystem>();
            owner.OnUpdate += Timers;
            entity = (EntityController)owner;
        }

        public void Timers()
        {
            if (_slideComponent.SlideProcess == null && (_jumpComponent.isGround || wallEdgeClimbComponent.EdgeStuckProcess != null))
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

                if (wallEdgeClimbComponent.EdgeStuckProcess != null)
                {
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            rb.linearVelocityX = dashDirection * Mathf.Abs(residualSpeed * _dashComponent.dashSlideForce);
            _moveSystem.IsActive = true;
            _dashComponent.ghostTrail.StopTrail();
            _dashComponent.isDash = false;
            yield return new WaitForSeconds(0.2f);
            _dashComponent.DashProcess = null;
        }
    }
}