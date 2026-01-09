using Assets.Scripts;
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

        protected override void Start()
        {
            base.Start();
        }
        public override void SelectItem(AbstractEntity owner)
        {
            base.SelectItem(owner);
            meleeWeaponSystem = new MeleeWeaponSystem();
            meleeWeaponSystem.Initialize(this);
            AddControllerSystem(meleeWeaponSystem);
            nonInitComponents.Add(typeof(MeleeComponent));
            contactDmgHits.Clear();
            meleeComponent.OnFirstHit.AddListener(BrokeWeapon);
        }
        public override void InitAfterSpawnFromInventory(Dictionary<System.Type, IComponent> invComponents)
        {
            nonInitComponents.Add(typeof(MeleeComponent));
            base.InitAfterSpawnFromInventory(invComponents);
        }

        public void BrokeWeapon(HitInfo _)
        {
            healthComponent.currHealth--;
        }

        protected override void ReferenceClean()
        {
            base.ReferenceClean();
            if (isSelected)
            {
                meleeComponent.OnFirstHit.RemoveListener(BrokeWeapon);
            }
        }
        private bool isAttacking = false;

        public unsafe override void Update()
        {
            base.Update();

            if (isSelected)
                return;
            bool shouldAttack = baseFields.rb.linearVelocity.magnitude > meleeComponent.velocityToDmg;

            if (shouldAttack)
            {
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
        public float velocityToDmg;
        
        public TrailRenderer trail;
        
        public PolygonCollider2D polygonCollider;
        public List<Vector2> points = new List<Vector2>();
        private Collider2D[] hits = new Collider2D[20];

        public UnityEvent<HitInfo> OnFirstHit;
        public Collider2D[] CheckObjectsInsideCollider(out int hitCount,Collider2D collider,LayerMask layerMask)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                hits[i] = null;
            }
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(layerMask);
                
            hitCount = collider.Overlap(filter, hits);

            return hits;
        }

        public void UpdateTrailGeometryCollider()
        {
            if (trail == null || polygonCollider == null)
                return;

            int count = trail.positionCount;
            if (count < 2)
                return;

            List<Vector2> upper = new(count);
            List<Vector2> lower = new(count);

            Transform t = polygonCollider.transform;

            float trailTime = trail.time;
            AnimationCurve widthCurve = trail.widthCurve;
            float startWidth = trail.startWidth;
            float endWidth = trail.endWidth;

            Vector2[] localPoints = new Vector2[count];

            for (int i = 0; i < count; i++)
            {
                localPoints[i] = t.InverseTransformPoint(trail.GetPosition(i));
            }

            for (int i = 0; i < count; i++)
            {
                Vector2 dir;

                if (i == 0)
                {
                    dir = (localPoints[1] - localPoints[0]).normalized;
                }
                else if (i == count - 1)
                {
                    dir = (localPoints[count - 1] - localPoints[count - 2]).normalized;
                }
                else
                {
                    Vector2 dirA = (localPoints[i] - localPoints[i - 1]).normalized;
                    Vector2 dirB = (localPoints[i + 1] - localPoints[i]).normalized;

                    dir = (dirA + dirB);
                    if (dir.sqrMagnitude < 0.0001f)
                        dir = dirB;
                    else
                        dir.Normalize();
                }

                Vector2 normal = new Vector2(-dir.y, dir.x);

                float t01 = count > 1 ? (float)i / (count - 1) : 0f;

                float curveWidth = widthCurve.Evaluate(t01);
                float width = Mathf.Lerp(startWidth, endWidth, t01) * curveWidth * 5;

                Vector2 offset = normal * width;

                upper.Add(localPoints[i] + offset);
                lower.Add(localPoints[i] - offset);
            }

            lower.Reverse();

            List<Vector2> colliderPath = new(upper.Count + lower.Count);
            colliderPath.AddRange(upper);
            colliderPath.AddRange(lower);

            polygonCollider.SetPath(0, colliderPath);
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
                return;

            _meleeComponent.UpdateTrailGeometryCollider();
            Collider2D[] hits = _meleeComponent.CheckObjectsInsideCollider(out var hitCount, _meleeComponent.polygonCollider, _weaponComponent.attackLayer);
            HitsDealer(hits, hitCount);

            for (int i = 0; i < _baseFields.collider.Length; i++)
            {
                hits = _meleeComponent.CheckObjectsInsideCollider(out hitCount, _baseFields.collider[i], _weaponComponent.attackLayer);

                HitsDealer(hits, hitCount);
            }
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
            HitInfo hitInfo = new HitInfo(hitPoint);
            new Damage(_weaponComponent.modifiedDamage, target.GetControllerComponent<ProtectionComponent>()).ApplyDamage(hs, hitInfo);

            var targetRb = target.GetControllerComponent<ControllersBaseFields>()?.rb;
            Vector2 dir = (target.mono.transform.position - transform.position).normalized;
            var totalForce = (dir.normalized * _meleeComponent.pushbackForce) + (Vector2.up * _meleeComponent.liftForce);

            targetRb?.AddForce(totalForce, ForceMode2D.Impulse);

            if (IsFirstHit)
            {
                FirstHit(target,col, hitInfo);
            }

            hitedList.Add(target.mono.gameObject);
        }

        protected virtual void FirstHit(AbstractEntity target, Collider2D col, HitInfo hitContext)
        {
            if (_itemComponent.currentOwner != null)
            {
                var selfRb = _itemComponent.currentOwner.GetControllerComponent<ControllersBaseFields>().rb;
                Vector2 dir = (target.mono.transform.position - transform.position).normalized;
                selfRb.AddForce(-dir * _meleeComponent.pushbackForce * 0.25f, ForceMode2D.Impulse);
            }

            _meleeComponent.OnFirstHit?.Invoke(hitContext);
        }

        public void Dispose()
        {
            owner.OnUpdate -= Update;
        }
    }
}