using System;
using System.Collections;
using Controllers;
using UnityEngine;

namespace Systems
{
    public class GroundingSystem: BaseSystem,IDisposable
    {
        private GroundingComponent _groundingComponent;
        private ControllersBaseFields _baseFields;
        private WallRunComponent _wallRunComponent;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _groundingComponent = owner.GetControllerComponent<GroundingComponent>();
            _baseFields = owner.GetControllerComponent<ControllersBaseFields>();
            _wallRunComponent = owner.GetControllerComponent<WallRunComponent>();
            owner.OnUpdate += OnUpdate;
            owner.OnGizmosUpdate += OnGizmosUpdate;
        }

        private void OnUpdate()
        {
            if((_wallRunComponent.wallRunProcess == null | _wallRunComponent == null))
                GroundCheack();
        }

        public void GroundCheack()
        {
            _groundingComponent.groundedColliders = Physics2D.OverlapBoxAll((Vector2)_baseFields.collider[0].bounds.center + Vector2.down * _baseFields.collider[0].bounds.extents.y,
                _groundingComponent.groundCheackSize,
                0,
                _groundingComponent.groundLayer);
            
            bool hasPlatform = false;
            bool hasRegularGround = false;

            foreach (var col in _groundingComponent.groundedColliders)
            {
                if (col == null) continue;

                if (col.TryGetComponent<PlatformEffector2D>(out _))
                    hasPlatform = true;
                else
                    hasRegularGround = true;
            }
            
            if (hasRegularGround)
            {
                _groundingComponent._platformGroundTime = _groundingComponent.platformGroundTime;
                _groundingComponent.IsReallyGrounded = true;
            }
            else if (hasPlatform)
            {
                if (_groundingComponent.IsOnPlatformLastFrame)
                {
                    _groundingComponent._platformGroundTime -= Time.deltaTime;
                }
                else
                {
                    // Только что зашли на платформу — начинаем отсчёт заново
                    _groundingComponent._platformGroundTime = _groundingComponent.platformGroundTime;
                }

                _groundingComponent.IsReallyGrounded = _groundingComponent._platformGroundTime <= 0f;
                _groundingComponent.IsOnPlatformLastFrame = true;
            }
            else
            {
                _groundingComponent._platformGroundTime = _groundingComponent.platformGroundTime;
                _groundingComponent.IsReallyGrounded = false;
                _groundingComponent.IsOnPlatformLastFrame = false;
            }
        }

        private void OnGizmosUpdate()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube((Vector2)_baseFields.collider[0].bounds.center + Vector2.down * _baseFields.collider[0].bounds.extents.y, _groundingComponent.groundCheackSize);
        }
        
        public void Dispose()
        {
            owner.OnUpdate -= OnUpdate;
            owner.OnGizmosUpdate -= OnGizmosUpdate;
        }
    }
    
    [System.Serializable]
    public class GroundingComponent : IComponent
    {
        public bool isGround => IsReallyGrounded;
        public Collider2D[] groundedColliders;
        public LayerMask groundLayer;
        public float platformGroundTime = 0.1f;
        [HideInInspector] public float _platformGroundTime = 0.1f;
        public Vector2 groundCheackSize;
        public bool IsReallyGrounded { get; set; } 
        [HideInInspector] public bool IsOnPlatformLastFrame = false;
    }
}