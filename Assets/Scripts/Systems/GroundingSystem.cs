using System;
using Controllers;
using UnityEngine;

namespace Systems
{
    public class GroundingSystem: BaseSystem,IDisposable
    {
        private GroundingComponent _groundingComponent;
        private ControllersBaseFields _baseFields;
        private WallRunComponent _wallRunComponent;
        private ContactFilter2D _filter;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _groundingComponent = owner.GetControllerComponent<GroundingComponent>();
            _baseFields = owner.GetControllerComponent<ControllersBaseFields>();
            _wallRunComponent = owner.GetControllerComponent<WallRunComponent>();

            _filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = _groundingComponent.groundLayer,
                useTriggers = false
            };

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
            var bounds = _baseFields.collider[0].bounds;
            var up = (Vector2)transform.up;
            var right = (Vector2)transform.right;
            var bottomCenter = (Vector2)bounds.center - up * bounds.extents.y;

            _groundingComponent.rayOrigins[0] = bottomCenter - right * bounds.extents.x;
            _groundingComponent.rayOrigins[1] = bottomCenter;
            _groundingComponent.rayOrigins[2] = bottomCenter + right * bounds.extents.x;

            var anyGrounded = false;

            for (int i = 0; i < 3; i++)
            {
                var count = Physics2D.Raycast(_groundingComponent.rayOrigins[i], -up, _filter, _groundingComponent.rayHits[i], _groundingComponent.rayLength);
                _groundingComponent.rayHitCounts[i] = count;

                if (count == 0)
                {
                    _groundingComponent.rayGrounded[i] = false;
                    continue;
                }

                var hit = _groundingComponent.rayHits[i][0];
                var grounded = hit.distance <= _groundingComponent.groundedThreshold;

                if(grounded && hit.collider.TryGetComponent<PlatformEffector2D>(out _))
                    grounded = IsValidPlatformHit(hit);

                _groundingComponent.rayGrounded[i] = grounded;

                if(grounded)
                    anyGrounded = true;
            }

            _groundingComponent.IsReallyGrounded = anyGrounded;
        }

        private bool IsValidPlatformHit(RaycastHit2D hit)
        {
            var platformRb = hit.collider.attachedRigidbody;
            var relativeVelocity = _baseFields.rb.linearVelocity - (platformRb != null ? platformRb.linearVelocity : Vector2.zero);
            return relativeVelocity.y <= 0f;
        }

        private void OnGizmosUpdate()
        {
            for (int i = 0; i < 3; i++)
            {
                Gizmos.color = _groundingComponent.rayGrounded[i] ? Color.green : Color.red;
                Gizmos.DrawLine(_groundingComponent.rayOrigins[i], _groundingComponent.rayOrigins[i] + (Vector2)(-transform.up) * _groundingComponent.rayLength);
            }
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
        public LayerMask groundLayer;
        public float rayLength = 1f;
        public float groundedThreshold = 0.05f;
        [NonSerialized] public Vector2[] rayOrigins = new Vector2[3];
        public Vector2 origin => rayOrigins[1];
        [NonSerialized] public RaycastHit2D[][] rayHits =
        {
            new RaycastHit2D[2],
            new RaycastHit2D[2],
            new RaycastHit2D[2]
        };
        [NonSerialized] public int[] rayHitCounts = new int[3];
        [NonSerialized] public bool[] rayGrounded = new bool[3];
        public bool IsReallyGrounded { get => isGround; set 
            {
                if (value)
                {
                    if(!isGround)
                        OnGround?.Invoke();
                }
                else
                {
                    if(isGround)
                        OnUnGround?.Invoke();
                }
                
                isGround = value;
            } }

        public Action OnGround;
        public Action OnUnGround;
    }
}