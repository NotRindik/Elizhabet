using Controllers;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Systems
{
    public class JumpSystem : BaseSystem
    {
        private JumpComponent jumpComponent;
        private EntityController _entityController;
        private AnimationComponent _animationComponent;

        private WallRunComponent _wallRunComponent;
        private Coroutine jumpBufferProcess;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _entityController = (EntityController)owner;
            jumpComponent = owner.GetControllerComponent<JumpComponent>();
            jumpComponent.coyotTime = jumpComponent._coyotTime;
            _animationComponent = owner.GetControllerComponent<AnimationComponent>();
            _wallRunComponent = owner.GetControllerComponent<WallRunComponent>();
            owner.OnUpdate += Update;
        }
        public override void OnUpdate()
        {
            TimerDepended();
            if(_wallRunComponent.wallRunProcess == null | _wallRunComponent == null)
                GroundCheack();
        }
        
        private void TimerDepended()
        {
            if (!jumpComponent.isGround)
            {
                if (jumpComponent.coyotTime > 0)
                    jumpComponent.coyotTime -= Time.deltaTime;
            }
            else
            {
                _entityController.baseFields.rb.gravityScale = 1;
                jumpComponent.coyotTime = jumpComponent._coyotTime;
            }
        }
        public bool TryJump()
        {
            if(IsActive == false)
                return false;
            if (jumpComponent.isGround || jumpComponent.coyotTime > 0)
            {
                Jump();
                return true;
            }
            
            if (!jumpComponent.isGround)
            {
                if (jumpBufferProcess == null) 
                    jumpBufferProcess = owner.StartCoroutine(JumpBufferProcess());
            }
            return false;
        }

        public void Jump()
        {
            if(IsActive == false)
                return;
            if (_animationComponent.currentState != "FallDown")
            {
                _animationComponent.CrossFade("FallDown",0.1f);
            }
            _entityController.baseFields.rb.linearVelocityY = 0;
            _entityController.baseFields.rb.AddForce(Vector2.up * jumpComponent.jumpForce, ForceMode2D.Impulse);
            owner.StartCoroutine(SetCoyotoTime(0));
        }

        public IEnumerator JumpBufferProcess()
        {
            jumpComponent.isJumpBufferSave = true;
            owner.StartCoroutine(JumpBufferUpdateProcess());
            yield return new WaitForSeconds(jumpComponent.jumpBufferTime);
            jumpComponent.isJumpBufferSave = false;
            jumpBufferProcess = null;
        }

        public IEnumerator SetCoyotoTime(float coyotoTime)
        {
            yield return new WaitUntil( () => jumpComponent.isGround == false);
            jumpComponent.coyotTime = coyotoTime;
        }

        public IEnumerator JumpBufferUpdateProcess()
        {
            while (jumpComponent.isJumpBufferSave)
            {
                if (jumpComponent.isGround)
                {
                    Jump();
                }
                yield return null;
            }
        }
        public void GroundCheack()
        {
            jumpComponent.isGround = Physics2D.OverlapBox((Vector2)_entityController.baseFields.collider.bounds.center + Vector2.down * _entityController.baseFields.collider.bounds.extents.y,
                jumpComponent.groundCheackSize,
                0,
                jumpComponent.groundLayer);
        }
        public void OnJumpUp()
        {
            if(IsActive == false)
                return;
            _entityController.baseFields.rb.AddForce(Vector2.down * _entityController.baseFields.rb.linearVelocityY * (1 - jumpComponent.JumpCutMultiplier), ForceMode2D.Impulse);
        }
    }
    
    [System.Serializable]
    public class JumpComponent : IComponent
    {
        public float jumpForce;
        public float jumpBufferTime;
        public float fallGravityMultiplier;
        [Range(0f, 1f)]
        public float JumpCutMultiplier;
        public float _coyotTime ;
        public float gravityScale;
        public Vector2 groundCheackSize;
        public LayerMask groundLayer;
        internal bool isGround;
        public bool isJump;
        public bool isJumpButtonPressed;
        public bool isJumpBufferSave;
        internal float coyotTime;
    }
}