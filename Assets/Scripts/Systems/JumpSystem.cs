using System;
using Controllers;
using System.Collections;
using Assets.Scripts;
using States;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using static UnityEngine.RuleTile.TilingRuleOutput;

namespace Systems
{
    public class JumpSystem : BaseSystem,IDisposable
    {
        private JumpComponent jumpComponent;
        private EntityController _entityController;
        private AnimationComponent _animationComponent;

        private GroundingComponent _groundingComponent;
        private Coroutine jumpBufferProcess;
        private ParticleComponent _particleComponent;
        private FSMSystem _fsm;
        public Vector2 oldVelocity;
        public float currVelocity;

        private bool _isCrash;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _entityController = (EntityController)owner;
            jumpComponent = owner.GetControllerComponent<JumpComponent>();
            jumpComponent.coyotTime = jumpComponent._coyotTime;
            _animationComponent = owner.GetControllerComponent<AnimationComponent>();
            _groundingComponent = owner.GetControllerComponent<GroundingComponent>();
            _particleComponent = owner.GetControllerComponent<ParticleComponent>();
            _fsm = owner.GetControllerSystem<FSMSystem>();
            owner.OnUpdate += Update;
            owner.OnFixedUpdate += OnFixedUpdate;
        }
        public override void OnUpdate()
        {
            TimerDepended();
        }
        public void OnFixedUpdate()
        {
            if(!IsActive)
                return;
            oldVelocity = _entityController.baseFields.rb.linearVelocity;
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
                jumpComponent.isJumpCuted = false;
                jumpComponent.isJump = false;
                jumpComponent.coyotTime = jumpComponent._coyotTime;
            }
            if (_groundingComponent.isGround && !_isCrash)
            {
                _isCrash = true;

                float impactStrength = Mathf.Abs(oldVelocity.y);
                int emitCount = Mathf.Clamp((int)(impactStrength * 7f), 0, 100);
                
                if (_groundingComponent.groundedColliders.Length != 0)
                {
                    
                    if (TryGetTileSpriteUnderFeet(_groundingComponent.groundedColliders[0],
                            _entityController.baseFields.collider[0].bounds.min,
                            out Sprite sprite))
                    {
                        if (_particleComponent.groundedParticle != null)
                        {
                            var textureSheetAnimation = _particleComponent.groundedParticle.textureSheetAnimation;
                            if (textureSheetAnimation.GetSprite(0) != sprite)
                                textureSheetAnimation.SetSprite(0, sprite);
                        }
                    }

                }
                if(_particleComponent.groundedParticle)
                    _particleComponent.groundedParticle.Emit(emitCount);
                if(emitCount != 0)AudioManager.instance.PlaySoundEffect($"{FileManager.SFX}Crash", volume: 0.5f);
            }
            else if(_groundingComponent.isGround == false)
            {
                _isCrash = false;
            }
        }
        public static bool TryGetTileSpriteUnderFeet(Collider2D groundCollider, Vector2 feetWorldPos, out Sprite sprite)
        {
            sprite = null;

            if (groundCollider == null)
                return false;

            Tilemap tilemap = groundCollider.GetComponentInParent<Tilemap>();
            if (tilemap == null)
                return false;

            // Чуть поднимаем позицию ступней, чтобы не попасть в шов
            feetWorldPos += Vector2.up * (tilemap.layoutGrid.cellSize.y * 0.1f);

            // Получаем клетку
            Vector3Int cellPos = tilemap.WorldToCell(feetWorldPos);

            // Новый способ: получаем уже отрисованный спрайт
            sprite = tilemap.GetSprite(cellPos);

            // Альтернатива: попробовать ниже, если вдруг пусто
            if (sprite == null)
                sprite = tilemap.GetSprite(cellPos + Vector3Int.down);

            return sprite != null;
        }
        public bool TryJump()
        {
            return false;
        }

        public void Jump()
        {
            if(IsActive == false)
                return;

            if (_animationComponent != null)
            {
                if (_animationComponent.currentState != "FallDown")
                {
                    _animationComponent.CrossFade("FallDown", 0.1f);
                }
            }
            jumpComponent.isJump = true;
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
            jumpComponent.isJumpCuted = true;
        }
        public void Dispose()
        {
            owner.OnUpdate -= Update;
            owner.OnFixedUpdate -= OnFixedUpdate;
        }
    }
    
    [System.Serializable]
    public class JumpComponent : IComponent
    {
        public float jumpForce;
        public float jumpBufferTime;
        [Range(0f, 1f)]
        public float JumpCutMultiplier;
        public float _coyotTime ;
        public float gravityScale;
        public Vector2 jumpDirection = Vector2.up;
        public bool isJumpBufferSave;
        public bool isJump,isJumpCuted;
        internal float coyotTime;
    }
}