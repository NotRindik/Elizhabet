using System;
using System.Collections.Generic;
using System.Linq;
using Systems;
using UnityEngine;
using UnityEngine.Events;

namespace Controllers
{
    using UnityEngine;

    public class MeleeWeapon : Weapon
    {
        protected MeleeComponent meleeComponent => GetControllerComponent<MeleeComponent>();
        protected AttackAnimationComponent attackAnimationComponent => GetControllerComponent<AttackAnimationComponent>();
        protected VelocityImpactComponent velocityImpactComponent => GetControllerComponent<VelocityImpactComponent>();

        protected override IComponent[] DefaultComponents =>
            base.DefaultComponents
                .Concat(new IComponent[]
                {
                    new MeleeComponent{trail = GetComponentInChildren<TrailRenderer>(),polygonCollider = GetComponentInChildren<PolygonCollider2D>()},
                    new AttackAnimationComponent(),
                    new VelocityImpactComponent()
                })
                .ToArray();

        protected override ISystem[] DefaultSystems =>
            base.DefaultSystems
                .Concat(new ISystem[]
                {
                    new MeleeWeaponSystem(),
                    new AttackAnimationSystem(),
                    new MeleeAttackTriggerSystem(),
                    new OnDemandAimSystem(),
                    new MeleeImpactSystem(),
                    new VelocityImpactSystem()
                })
                .ToArray();
    }
    [System.Serializable]
public class MeleeComponent : IComponent
{
    public float attackSpeed;
    public float pushbackForce = 10f;
    public float liftForce = 3f;
    
    public bool IsDamageState;

    
    public TrailRenderer trail;
    public PolygonCollider2D polygonCollider;
    public List<Vector2> points = new List<Vector2>();
    
    private Collider2D[] _hits = new Collider2D[20];
    public UnityEvent<HitInfo> OnFirstHit;
    
    private Transform _colliderTransform;
    private AnimationCurve _widthCurve;
    private float _cachedStartWidth;
    private float _cachedEndWidth;
    private int _lastPositionCount = -1;
    
    public const float PogoHeight = 3.3f;
    
    private List<Vector2> _upper = new List<Vector2>(128);
    private List<Vector2> _lower = new List<Vector2>(128);
    private List<Vector2> _colliderPath = new List<Vector2>(256);

    protected Mesh _trailMesh;

    public Collider2D[] CheckObjectsInsideCollider(out int hitCount, Collider2D collider, LayerMask layerMask)
    {
        for (int i = 0; i < _hits.Length; i++)
            _hits[i] = null;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(layerMask);
        hitCount = collider.Overlap(filter, _hits);
        return _hits;
    }
    
    public void CheckObjectsInsideCollider(Collider2D col, LayerMask layer, List<Collider2D> results)
    {
        ContactFilter2D filter = new ContactFilter2D();
        
        filter.SetLayerMask(layer);
        int count = Physics2D.OverlapCollider(col, filter, _hits);

        for (int i = 0; i < count; i++)
        {
            results.Add(_hits[i]);
        }
    }

    public void ClearCollider()
    {
        trail.Clear();
        polygonCollider.pathCount = 0;
        _lastPositionCount = -1;
    }

    public void UpdateTrailGeometryCollider()
    {
        if (trail == null || polygonCollider == null)
            return;

        if (_colliderTransform == null)
        {
            _colliderTransform = polygonCollider.transform;
        }

        int count = trail.positionCount;

        if (!trail.emitting)
        {
            polygonCollider.pathCount = 0;
            _lastPositionCount = -1;
            return;
        }

        if (count == _lastPositionCount)
            return;
        _lastPositionCount = count;
        
        if (_trailMesh == null)
            _trailMesh = new Mesh();
    
        trail.BakeMesh(_trailMesh, ContextManager.Instance.mainCamera, true);

        Vector3[] vertices = _trailMesh.vertices;
        
        int pairCount = vertices.Length / 2;

        const float cutoffT = 0.15f;
        int startIndex = Mathf.FloorToInt(cutoffT * (pairCount - 1));
        int validCount = pairCount - startIndex;

        if (validCount < 2)
        {
            polygonCollider.pathCount = 0;
            return;
        }

        _upper.Clear();
        _lower.Clear();

        for (int i = 0; i < validCount; i++)
        {
            int meshIndex = (i + startIndex);
            // Меш трейла: чётные = одна сторона, нечётные = другая
            Vector3 v0 = vertices[meshIndex * 2];
            Vector3 v1 = vertices[meshIndex * 2 + 1];

            Vector2 p0 = _colliderTransform.InverseTransformPoint(v0);
            Vector2 p1 = _colliderTransform.InverseTransformPoint(v1);

            _upper.Add(p0);
            _lower.Add(p1);
        }

        _lower.Reverse();

        _colliderPath.Clear();
        _colliderPath.AddRange(_upper);
        _colliderPath.AddRange(_lower);

        polygonCollider.pathCount = 1;
        polygonCollider.SetPath(0, _colliderPath);
    }
}
    public class MeleeWeaponSystem : BaseSystem, IDisposable
    {
        protected HashSet<GameObject> hitedList = new HashSet<GameObject>();
        protected WeaponComponent _weaponComponent;
        protected ItemComponent _itemComponent;
        protected MeleeComponent _meleeComponent;
        
        protected ControllersBaseFields _baseFields;

        protected List<Collider2D> hitCols = new (15);
        protected bool IsFirstHit => hitedList.Count == 0;
        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _meleeComponent = base.owner.GetControllerComponent<MeleeComponent>();
            _weaponComponent = base.owner.GetControllerComponent<WeaponComponent>();
            _itemComponent = owner.GetControllerComponent<ItemComponent>();
            _baseFields = owner.GetControllerComponent<ControllersBaseFields>();

            owner.OnUpdate += Update;
        }

        public void BeginDamage()
        {
            hitedList.Clear();
            
            _meleeComponent.IsDamageState = true;
            _meleeComponent.trail.emitting = true;
        }

        public void EndDamage()
        {
            _meleeComponent.trail.emitting = false;
            _meleeComponent.IsDamageState = false;
            _meleeComponent.ClearCollider();
        }


        public override void OnUpdate()
        {
            if (!_meleeComponent.IsDamageState)
            {
                return;
            }


            _meleeComponent.UpdateTrailGeometryCollider();
            
            
            _meleeComponent.CheckObjectsInsideCollider(_meleeComponent.polygonCollider, _weaponComponent.attackLayer, hitCols);

            for (int i = 0; i < _baseFields.collider.Length; i++)
            {
                _meleeComponent.CheckObjectsInsideCollider(_baseFields.collider[i], _weaponComponent.attackLayer, hitCols);
            }
            
            HitsDealer(hitCols.ToArray(), hitCols.Count);
            hitCols.Clear();
        }

        private void HitsDealer(Collider2D[] hits, int hitCount)    
        {
            for (int j = 0; j < hitCount; j++)
            {
                if (hits[j].TryGetComponent(out AbstractEntity controller))
                {
                    if (!hitedList.Contains(controller.mono.gameObject))
                    {
                         DealDamage(controller, hits[j]);
                    }
                }
            }
        }

        protected virtual void DealDamage(AbstractEntity target, Collider2D col) 
        {
            Vector2 hitDir = (target.mono.transform.position - transform.position).normalized;
            Vector2 hitPoint = col.ClosestPoint(transform.position);

            var hs = target.GetControllerSystem<HealthSystem>();
            if(!hs.IsActive)
                return;
            
            HitInfo hitInfo = new HitInfo() 
            {
                Attacker = _itemComponent.currentOwner == null ? owner : _itemComponent.currentOwner,
                Target = target,
                hitPosition = hitPoint
            };
            if(hs != null)
                new Damage(_weaponComponent.GetFullDamage(), target.GetControllerComponent<ProtectionComponent>()).ApplyDamage(hs, ref hitInfo);

            var targetRb = target.GetControllerComponent<ControllersBaseFields>()?.rb;
            Vector2 dir = (target.mono.transform.position - transform.position).normalized;
            var totalForce = (dir.normalized * _meleeComponent.pushbackForce) + (Vector2.up * 2);

            targetRb?.AddForce(totalForce, ForceMode2D.Impulse);

            if (IsFirstHit)
            {
                FirstHit(hitInfo);
            }

            hitedList.Add(target.mono.gameObject);
        }

        protected virtual void FirstHit(HitInfo hitContext)
        {
            _meleeComponent.OnFirstHit?.Invoke(hitContext);
        }

        public void Dispose()
        {
            owner.OnUpdate -= Update;
        }
    }
}