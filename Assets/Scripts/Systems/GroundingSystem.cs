using System;
using Controllers;
using UnityEngine;

namespace Systems
{
    public class GroundingSystem : BaseSystem, IDisposable
    {
        private GroundingComponent _groundingComponent;
        private ControllersBaseFields _baseFields;
        private WallRunComponent _wallRunComponent;
        private ContactFilter2D _filter;

        private readonly int[] order = { 1, 0, 2 };
        private float _graceTimer;

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
                if (_wallRunComponent.wallRunProcess == null)
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

            float skin = _groundingComponent.rayStartOffset;
            float length = _groundingComponent.rayLength + skin;

            _groundingComponent.rayOrigins[0] = bottomCenter - right * bounds.extents.x;
            _groundingComponent.rayOrigins[1] = bottomCenter;
            _groundingComponent.rayOrigins[2] = bottomCenter + right * bounds.extents.x;

            var anyGrounded = false;

            for (int i = 0; i < 3; i++)
            {
                var hits = _groundingComponent.rayHits[i];
                Vector2 start = _groundingComponent.rayOrigins[i] + up * skin;

                var count = Physics2D.Raycast(start, -up, _filter, hits, length);
                _groundingComponent.rayHitCounts[i] = count;
                _groundingComponent.rayHasHit[i] = false;

                var found = -1;
                for (int j = 0; j < count; j++)
                {
                    var c = hits[j].collider;
                    if (c == null || c.attachedRigidbody == _baseFields.rb)
                        continue;

                    found = j;
                    break;
                }

                if (found < 0)
                {
                    _groundingComponent.rayGrounded[i] = false;
                    continue;
                }

                var hit = hits[found];
                _groundingComponent.rayBest[i] = hit;
                _groundingComponent.rayHasHit[i] = true;

                var distance = hit.distance - skin;

                var wasGrounded = _groundingComponent.rayGrounded[i];
                var grounded = wasGrounded
                    ? distance <= _groundingComponent.ungroundedThreshold
                    : distance <= _groundingComponent.groundedThreshold;

                if (grounded && hit.collider.TryGetComponent<PlatformEffector2D>(out _))
                    grounded = IsValidPlatformHit(hit);

                _groundingComponent.rayGrounded[i] = grounded;

                if (grounded)
                    anyGrounded = true;
            }

            if (anyGrounded)
            {
                _graceTimer = _groundingComponent.ungroundGraceTime;
            }
            else if (_groundingComponent.isGround &&
                     _graceTimer > 0f &&
                     _baseFields.rb.linearVelocity.y <= 0.1f)
            {
                _graceTimer -= Time.fixedDeltaTime;
                anyGrounded = true;
            }
            else
            {
                _graceTimer = 0f;
            }

            _groundingComponent.IsReallyGrounded = anyGrounded;

            CalcGroundNormal();
        }

        public void CalcGroundNormal()
        {
            _groundingComponent.groundNormal = Vector2.up;

            foreach (int i in order)
            {
                if (!_groundingComponent.rayHasHit[i])
                    continue;

                _groundingComponent.groundNormal = _groundingComponent.rayBest[i].normal;
                return;
            }
        }

        private bool IsValidPlatformHit(RaycastHit2D hit)
        {
            var platformRb = hit.collider.attachedRigidbody;
            var relativeVelocity = _baseFields.rb.linearVelocity - (platformRb != null ? platformRb.linearVelocity : Vector2.zero);

            return Vector2.Dot(relativeVelocity, hit.normal) <= 0f;
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
        public float rayStartOffset = 0.05f;
        public float groundedThreshold = 0.05f;
        public float ungroundedThreshold = 0.12f;
        public float ungroundGraceTime = 0.08f;

        [NonSerialized] public Vector2[] rayOrigins = new Vector2[3];
        public Vector2 origin => rayOrigins[1];
        [NonSerialized] public RaycastHit2D[][] rayHits =
        {
            new RaycastHit2D[4],
            new RaycastHit2D[4],
            new RaycastHit2D[4]
        };
        [NonSerialized] public RaycastHit2D[] rayBest = new RaycastHit2D[3];
        [NonSerialized] public bool[] rayHasHit = new bool[3];
        [NonSerialized] public int[] rayHitCounts = new int[3];
        [NonSerialized] public bool[] rayGrounded = new bool[3];
        public bool IsReallyGrounded
        {
            get => isGround;
            set
            {
                if (value)
                {
                    if (!isGround)
                        OnGround?.Invoke();
                }
                else
                {
                    if (isGround)
                        OnUnGround?.Invoke();
                }

                isGround = value;
            }
        }

        public Action OnGround;
        public Action OnUnGround;

        public Vector2 groundNormal = Vector2.up;
    }
}