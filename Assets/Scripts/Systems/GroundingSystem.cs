using System;
using System.Collections;
using Controllers;
using UnityEngine;
using UnityEngine.UIElements;

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
            if (_wallRunComponent != null)
            {
                if(_wallRunComponent.wallRunProcess == null)
                    GroundCheack();   
            }
            else
            {
                GroundCheack(); 
            }
            
        }

        public void GroundCheack()
        {
            _groundingComponent.origin = _baseFields.collider[0].bounds.center + (-owner.transform.up) * _baseFields.collider[0].bounds.extents.y;
            _groundingComponent.groundedColliders = Physics2D.OverlapBoxAll(
                _groundingComponent.origin,
                _groundingComponent.groundCheackSize,
                owner.transform.eulerAngles.z,
                _groundingComponent.groundLayer);

            bool hasPlatform = false;
            bool hasRegularGround = false;

            Collider2D platformCollider = null;

            foreach (var col in _groundingComponent.groundedColliders)
            {
                if (col == null) continue;

                if (col.TryGetComponent<PlatformEffector2D>(out _))
                {
                    platformCollider = col;
                    hasPlatform = true;
                }
                else
                {
                    hasRegularGround = true;
                }
            }

            if (hasRegularGround)
            {
                _groundingComponent.IsReallyGrounded = true;
            }
            else if (hasPlatform)
            {
                Vector2 feetPos = _baseFields.collider[0].bounds.min;
                float playerFeetY = feetPos.y;
                Vector2 platformPoint = platformCollider.ClosestPoint(feetPos);
                float platformTop = platformPoint.y;

                if (playerFeetY >= platformTop + _groundingComponent.platformTopOffset && Mathf.Abs(_baseFields.rb.linearVelocityY) < 0.4f)
                {
                    _groundingComponent.IsReallyGrounded = true;
                }
                else
                {
                    _groundingComponent.IsReallyGrounded = false;
                }
            }
            else
            {
                _groundingComponent.IsReallyGrounded = false;
            }
        }


        private void OnGizmosUpdate()
        {
            Gizmos.color = Color.red;

            Matrix4x4 defaultMatrix = Gizmos.matrix;

// устанавливаем матрицу в позицию и поворот объекта
            Gizmos.matrix = Matrix4x4.TRS(
                _baseFields.collider[0].bounds.center + (-owner.transform.up) * _baseFields.collider[0].bounds.extents.y,
                Quaternion.Euler(0, 0, owner.transform.eulerAngles.z),
                Vector3.one);

// рисуем "локальный" куб (0,0) с указанным размером
            Gizmos.DrawWireCube(Vector3.zero, _groundingComponent.groundCheackSize);

// возвращаем матрицу
            Gizmos.matrix = defaultMatrix;
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
        public Vector2 groundCheackSize;
        public float platformTopOffset = 0.001f;
        public Vector2 origin;
        public bool IsReallyGrounded { get; set; } 
    }
}