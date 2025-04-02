using Controllers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Systems
{
    public class JumpSystem : BaseSystem
    {
        private JumpComponent jumpComponent;
        private EntityController _entityController;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _entityController = (EntityController)owner;
            jumpComponent = owner.GetControllerComponent<JumpComponent>();
            jumpComponent.coyotTime = jumpComponent._coyotTime;
            jumpComponent.jumpBufer = jumpComponent._jumpbufer;
        }
        public override void Update()
        {
            jumpComponent.isGround = Physics2D.OverlapBox((Vector2)_entityController.baseFields.collider.bounds.center + Vector2.down * _entityController.baseFields.collider.bounds.extents.y,
                jumpComponent.groundCheackSize,
                0,
                jumpComponent.groundLayer);
            TimerDepended();
            GravityScale();
        }
        
        private void TimerDepended()
        {
            if (!jumpComponent.isGround)
            {
                if (jumpComponent.coyotTime > 0)
                    jumpComponent.coyotTime -= Time.deltaTime;
                if (jumpComponent.jumpBufer > 0 && jumpComponent.isJumpPressed)
                    jumpComponent.jumpBufer -= Time.deltaTime;
                else
                    jumpComponent.isJumpPressed = false;
            }
            else
            {
                if (jumpComponent.isJumpPressed)
                {
                    Jump(new InputAction.CallbackContext());
                    jumpComponent.isJumpPressed = false;
                }
                _entityController.baseFields.rb.gravityScale = 1;
                jumpComponent.jumpBufer = jumpComponent._jumpbufer;
                jumpComponent.coyotTime = jumpComponent._coyotTime;
            }
        }
        public void Jump(InputAction.CallbackContext callback)
        {
            if (jumpComponent.isGround)
            {
                _entityController.baseFields.rb.linearVelocityY = 0;
                _entityController.baseFields.rb.AddForce(Vector2.up * jumpComponent.jumpForce, ForceMode2D.Impulse);
            }
            else
                jumpComponent.isJumpPressed = true;
            jumpComponent.coyotTime = 0;
        }
        
        private void GravityScale()
        {
            if (_entityController.baseFields.rb.linearVelocityY < 0)
            {
                _entityController.baseFields.rb.gravityScale = jumpComponent.gravityScale * jumpComponent.fallGravityMultiplier;
            }
            else
            {
                _entityController.baseFields.rb.gravityScale = jumpComponent.gravityScale;
            }
        }
        public void OnJumpUp(InputAction.CallbackContext callback)
        {
            if (_entityController.baseFields.rb.linearVelocityY > 0)
            {
                _entityController.baseFields.rb.AddForce(Vector2.down * _entityController.baseFields.rb.linearVelocityY * (1 - jumpComponent.JumpCutMultiplier), ForceMode2D.Impulse);
            }
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
        public float _jumpbufer;
        public float gravityScale;
        public Vector2 groundCheackSize;
        public LayerMask groundLayer;
        public bool isJumpPressed;
        internal bool isGround;

        internal float coyotTime;
        internal float jumpBufer;
    }
}