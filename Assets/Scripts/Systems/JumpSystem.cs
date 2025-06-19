using Controllers;
using System.Collections;
using Assets.Scripts;
using States;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Systems
{
    public class JumpSystem : BaseSystem
    {
        private JumpComponent jumpComponent;
        private EntityController _entityController;
        private AnimationComponent _animationComponent;

        private GroundingComponent _groundingComponent;
        private Coroutine jumpBufferProcess;
        private FSMSystem _fsm;

        private bool _isCrash;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _entityController = (EntityController)owner;
            jumpComponent = owner.GetControllerComponent<JumpComponent>();
            jumpComponent.coyotTime = jumpComponent._coyotTime;
            _animationComponent = owner.GetControllerComponent<AnimationComponent>();
            _groundingComponent = owner.GetControllerComponent<GroundingComponent>();
            _fsm = owner.GetControllerSystem<FSMSystem>();
            owner.OnUpdate += Update;
        }
        public override void OnUpdate()
        {
            TimerDepended();
        }
        
        private void TimerDepended()
        {
            if (!_groundingComponent.isGround)
            {
                if (jumpComponent.coyotTime > 0)
                    jumpComponent.coyotTime -= Time.deltaTime;
            }
            else
            {
                _entityController.baseFields.rb.gravityScale = 1;
                jumpComponent.coyotTime = jumpComponent._coyotTime;
            }
            
            if (_groundingComponent.isGround && !_isCrash)
            {
                _isCrash = true;
                AudioManager.instance.PlaySoundEffect($"{FileManager.SFX}Crash",volume:0.5f);
            }
            else if(_groundingComponent.isGround == false)
            {
                _isCrash = false;
            }
        }
        public bool TryJump()
        {
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
            _entityController.baseFields.rb.AddForce(jumpComponent.jumpDirection * jumpComponent.jumpForce, ForceMode2D.Impulse);
            owner.StartCoroutine(SetCoyotoTime(0));
        }

        public void StartJumpBuffer()
        {
            if (!_groundingComponent.isGround)
            {
                if (jumpBufferProcess == null) 
                    jumpBufferProcess = owner.StartCoroutine(JumpBufferProcess());
            }
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
            yield return new WaitUntil( () => _groundingComponent.isGround == false);
            jumpComponent.coyotTime = coyotoTime;
        }

        public IEnumerator JumpBufferUpdateProcess()
        {
            while (jumpComponent.isJumpBufferSave)
            {
                if (_groundingComponent.isGround)
                {
                    _fsm.SetState(new JumpState((PlayerController)owner));
                }
                yield return null;
            }
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
        public Vector2 jumpDirection = Vector2.up;
        public bool isJumpBufferSave;
        public bool isJump;
        internal float coyotTime;
    }
}