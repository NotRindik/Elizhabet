using System;
using System.Collections.Generic;
using Systems;
using UnityEngine;
using UnityEngine.Events;

namespace Controllers
{
    public class MeleeWeapon : Weapon
    {
        public MeleeComponent meleeComponent = new MeleeComponent();
        public MeleeWeaponSystem meleeWeaponSystem;
        public List<AbstractEntity> contactDmgHits = new List<AbstractEntity>();
        public ComboComponent comboComponent = new ComboComponent(); 
        protected override void Start()
        {
            base.Start();

            DestroyCondition = () => attackComponent.isAttackFrameThisFrame == false;
        }
        public override void SelectItem(AbstractEntity owner)
        {
            base.SelectItem(owner);

            if (!ExistSys<MeleeWeaponSystem>())
            {
                meleeWeaponSystem = new MeleeWeaponSystem();
                meleeWeaponSystem.Initialize(this);
                AddControllerSystem(meleeWeaponSystem);   
            }
            
            nonInitComponents.Add(typeof(MeleeComponent));
            contactDmgHits.Clear();
            
            meleeWeaponSystem?.EndDamage();
            meleeComponent.OnFirstHit.AddListener(OnFirstHit);
        }
        public override void InitAfterSpawnFromInventory(Dictionary<System.Type, IComponent> invComponents)
        {
            nonInitComponents.Add(typeof(MeleeComponent));
            base.InitAfterSpawnFromInventory(invComponents);
        }
        public void OnFirstHit(HitInfo hit)
        {
            if(hit.Target.ExistSys<HealthSystem>() && hit.Target.GetControllerComponent<HealthComponent>().currHealth > 0)
                healthComponent.currHealth--;
             
            SelfKnockBack(hit);

            if (healthComponent.currHealth <= 0)
                DestroyItem();
        }

        private void SelfKnockBack(HitInfo hit)
        {
            if (itemComponent.currentOwner == null)
                return;

            var selfRb = hit.Attacker.GetControllerComponent<ControllersBaseFields>().rb;

            Vector2 dir = ((Vector2)hit.Target.mono.transform.position - 
                           (Vector2)hit.Attacker.transform.position).normalized;

            float similarity = Vector2.Dot(dir, Vector2.down);

            bool isPlayerInAir = Mathf.Abs(selfRb.linearVelocityY) > 0.3f;
            bool isTargetBelow = hit.Target.mono.transform.position.y < hit.Attacker.transform.position.y - 0.1f;

            attackComponent.IsPogo = similarity > 0.6f && isPlayerInAir && isTargetBelow;

            if (attackComponent.IsPogo)
            {
                TimeManager.StartHitStop(0.02f, 0.1f);

                float gravity = Mathf.Abs(Physics2D.gravity.y * selfRb.gravityScale);
                
                float targetHeightAboveEnemy = MeleeComponent.PogoHeight;

                float enemyY = hit.Target.mono.transform.position.y;
                float playerY = hit.Attacker.transform.position.y;
                
                float heightToReach = (enemyY + targetHeightAboveEnemy) - playerY;
                
                float requiredVelocity = heightToReach > 0
                    ? Mathf.Sqrt(2f * gravity * heightToReach)
                    : Mathf.Sqrt(2f * gravity * targetHeightAboveEnemy);

                selfRb.linearVelocityY = 0;
                selfRb.linearVelocityY = requiredVelocity;
            }
            else
            {
                selfRb.linearVelocityY = 0;
                selfRb.AddForce(meleeComponent.pushbackForce * 0.25f * Vector2.up, ForceMode2D.Impulse);
            }
        }

        protected override void ReferenceClean()
        {
            if (isSelected)
            {
                meleeComponent.OnFirstHit.RemoveListener(OnFirstHit);
            }
            base.ReferenceClean();
        }
        
        
        private bool isAttacking = false;
        

        public override void Update()
        {
            base.Update();

            if (isSelected)
            {
                isAttacking = false;
                return;
            }
            bool shouldAttack = baseFields.rb.linearVelocity.magnitude > MeleeComponent.VelocityToDmg;
            
            if (shouldAttack && isAttacking == false) {
                meleeWeaponSystem?.BeginDamage();
                isAttacking = true;
            }
            else if (!shouldAttack && isAttacking)
            {
                meleeWeaponSystem?.EndDamage();
                isAttacking = false;
            }
        }
    }

    [System.Serializable]
public class MeleeComponent : IComponent
{
    public float attackSpeed;
    public float pushbackForce = 10f;
    public float liftForce = 3f;
    public const float VelocityToDmg = 2;
    
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
        protected AttackComponent _attackComponent;
        protected HealthComponent _healthComponent;
        protected ControllersBaseFields _baseFields;


        protected List<Collider2D> hitCols = new (15);
        protected bool IsFirstHit => hitedList.Count == 0;
        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _meleeComponent = base.owner.GetControllerComponent<MeleeComponent>();
            _attackComponent = base.owner.GetControllerComponent<AttackComponent>();
            _healthComponent = base.owner.GetControllerComponent<HealthComponent>();
            _weaponComponent = base.owner.GetControllerComponent<WeaponComponent>();
            _itemComponent = owner.GetControllerComponent<ItemComponent>();
            _baseFields = owner.GetControllerComponent<ControllersBaseFields>();

            owner.OnUpdate += Update;
        }

        public void BeginDamage()
        {
            hitedList.Clear();
            _attackComponent.isAttackFrameThisFrame = true;
        }

        public void EndDamage()
        {
            _attackComponent.isAttackFrameThisFrame = false;
        }


        public override void OnUpdate()
        {
            if (!_attackComponent.isAttackFrameThisFrame)
            {
                _meleeComponent.ClearCollider();
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
                new Damage(_weaponComponent.modifiedDamage, target.GetControllerComponent<ProtectionComponent>()).ApplyDamage(hs, hitInfo);

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