using System.Collections;
using Controllers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Systems
{
    public class DashSystem : BaseSystem
    {

        private DashComponent _dashComponent;
        private MoveComponent _moveComponent;
        private JumpComponent _jumpComponent;
        private SlideComponent _slideComponent;
        private AnimationComponent animationComponent;
        private WallEdgeClimbComponent wallEdgeClimbComponent;
        private EntityController entity;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _dashComponent = owner.GetControllerComponent<DashComponent>();
            _moveComponent = owner.GetControllerComponent<MoveComponent>();
            _jumpComponent = owner.GetControllerComponent<JumpComponent>();
            _slideComponent = owner.GetControllerComponent<SlideComponent>();
            animationComponent = owner.GetControllerComponent<AnimationComponent>();
            wallEdgeClimbComponent = owner.GetControllerComponent<WallEdgeClimbComponent>();
            owner.OnUpdate += Timers;
            entity = (EntityController)owner;
        }

        public void Timers()
        {
            if ((_jumpComponent.isGround || wallEdgeClimbComponent.EdgeStuckProcess != null) && _slideComponent.SlideProcess == null)
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
            if (_dashComponent.DashProcess == null && wallEdgeClimbComponent.EdgeStuckProcess == null && _dashComponent.allowDash == true)
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

            Vector2 startPos = rb.position;
            Vector2 targetPos = startPos + Vector2.right * dashDirection * dashDistance;

            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;

            float elapsed = 0f;
            animationComponent.CrossFade("FallUp",0.1f);
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
                
                if (_dashComponent.allowCancel &&
                    Mathf.Sign(_moveComponent.direction.x) != dashDirection &&
                    _moveComponent.direction.x != 0)
                {
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
            
            rb.gravityScale = _dashComponent.defaultGravityScale;
            _dashComponent.ghostTrail.StopTrail();
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