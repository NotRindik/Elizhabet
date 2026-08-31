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
        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _groundingComponent = owner.GetControllerComponent<GroundingComponent>();
            _baseFields = owner.GetControllerComponent<ControllersBaseFields>();
            _wallRunComponent = owner.GetControllerComponent<WallRunComponent>();
            owner.OnFixedUpdate += OnUpdate;
            owner.OnGizmosUpdate += OnGizmosUpdate;
        }

        public override void OnUpdate()
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
            _groundingComponent.origin = _baseFields.collider[0].bounds.center + (-transform.up) * _baseFields.collider[0].bounds.extents.y;
            ContactFilter2D filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = _groundingComponent.groundLayer
            };

            _groundingComponent.count = Physics2D.OverlapBox(
                _groundingComponent.origin,
                _groundingComponent.groundCheackSize,
                transform.eulerAngles.z,
                filter,
                _groundingComponent.groundedColliders
            );

            bool hasPlatform = false;
            bool hasRegularGround = false;

            Collider2D platformCollider = null;

            for (int i = 0; i < _groundingComponent.count; i++)
            {
                var col = _groundingComponent.groundedColliders[i];
                
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
                Vector2 relativeVelocity =
                    _baseFields.rb.linearVelocity -
                    (platformCollider.attachedRigidbody != null
                        ? platformCollider.attachedRigidbody.linearVelocity
                        : Vector2.zero);

                float feetY = _baseFields.collider[0].bounds.min.y;
                float platformCenterY = platformCollider.bounds.center.y;

                _groundingComponent.IsReallyGrounded =
                    feetY >= platformCenterY &&
                    relativeVelocity.y <= 0f;
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
            
            Gizmos.matrix = Matrix4x4.TRS(
                _baseFields.collider[0].bounds.center + (-transform.up) * _baseFields.collider[0].bounds.extents.y,
                Quaternion.Euler(0, 0, transform.eulerAngles.z),
                Vector3.one);
            
            Gizmos.DrawWireCube(Vector3.zero, _groundingComponent.groundCheackSize);
            
            Gizmos.matrix = defaultMatrix;
        }
        
        public void Dispose()
        {
            owner.OnFixedUpdate -= OnUpdate;
            owner.OnGizmosUpdate -= OnGizmosUpdate;
        }
    }
    
    [System.Serializable]
    public class GroundingComponent : IComponent
    {
        public bool isGround;
        [NonSerialized] public Collider2D[] groundedColliders = new Collider2D[3];
        [NonSerialized] public int count;
        public LayerMask groundLayer;
        public Vector2 groundCheackSize;
        public float platformTopOffset = 0.001f;
        public Vector2 origin;
        public bool IsReallyGrounded { get => isGround; set 
            {
                if(value == true)OnGround?.Invoke();
                else OnUnGround?.Invoke();
                isGround = value;
            } }

        public Action OnGround;
        public Action OnUnGround;
    }
}