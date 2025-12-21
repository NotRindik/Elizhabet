using Assets.Scripts;
using System.Collections;
using System.Collections.Generic;
using Systems;
using UnityEngine;

namespace Controllers
{
    public class MeleeWeapon : Weapon
    {
        public MeleeComponent meleeComponent = new MeleeComponent();
        public MeleeWeaponSystem meleeWeaponSystem;
        public List<IController> contactDmgHits = new List<IController>();

        protected override void Start()
        {
            base.Start();
        }
        public override void SelectItem(Controller owner)
        {
            base.SelectItem(owner);
            meleeWeaponSystem = new MeleeWeaponSystem();
            meleeWeaponSystem.Initialize(this);
            AddControllerSystem(meleeWeaponSystem);
            nonInitComponents.Add(typeof(MeleeComponent));
            contactDmgHits.Clear();
        }
        public override void InitAfterSpawnFromInventory(Dictionary<System.Type, IComponent> invComponents)
        {
            nonInitComponents.Add(typeof(MeleeComponent));
            base.InitAfterSpawnFromInventory(invComponents);
        }

        protected override void ReferenceClean()
        {
            base.ReferenceClean();
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
                for (int i = 0; i < baseFields.collider.Length; i++)
                {
                    Collider2D[] hitColliders = meleeComponent.CheckObjectsInsideCollider(out var hitCount, baseFields.collider[i], weaponComponent.attackLayer);
                    for (int j = 0; j < hitCount; j++)
                    {
                        if (hitColliders[j].TryGetComponent(out IController controller))
                        {
                            if (contactDmgHits.Contains(controller))
                                return;
                            contactDmgHits.Add(controller);
                            Vector2 hitDir = (controller.mono.transform.position - transform.position).normalized;
                            Vector2 hitPoint = hitColliders[j].ClosestPoint(transform.position);

                            AudioManager.instance.PlaySoundEffect($"{FileManager.SFX}hitHurt{Random.Range(1, 4)}", volume: 0.5f);
                            var hs = controller.GetControllerSystem<HealthSystem>();
                            new Damage(weaponComponent.modifiedDamage, controller.GetControllerComponent<ProtectionComponent>()).ApplyDamage(hs, new HitInfo(hitPoint));

                            var targetRb = controller.GetControllerComponent<ControllersBaseFields>()?.rb;
                            Vector2 dir = (controller.mono.transform.position - transform.position).normalized;
                            var totalForce = (dir.normalized * meleeComponent.pushbackForce) + (Vector2.up * meleeComponent.liftForce);
                            targetRb?.AddForce(totalForce, ForceMode2D.Impulse);

                        }
                    }
                }
                isAttacking = true;
            }
            else if (!shouldAttack && isAttacking)
            {
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
        private Collider2D[] hits = new Collider2D[10];


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
    }
    
    public class MeleeWeaponSystem: BaseSystem,IStopCoroutineSafely
    {
        protected List<GameObject> hitedList = new List<GameObject>();
        protected WeaponComponent _weaponComponent;

        protected MeleeComponent _meleeComponent;
        protected AttackComponent _attackComponent;
        protected HealthComponent _healthComponent;
        protected SpriteFlipSystem _spriteFlipSystem;
        protected ControllersBaseFields _baseFields;

        public override void Initialize(IController owner)
        {
            base.Initialize(owner);
            _meleeComponent = base.owner.GetControllerComponent<MeleeComponent>();
            _attackComponent = base.owner.GetControllerComponent<AttackComponent>();
            _healthComponent = base.owner.GetControllerComponent<HealthComponent>();
            _weaponComponent = base.owner.GetControllerComponent<WeaponComponent>();
            _spriteFlipSystem = owner.GetControllerSystem<SpriteFlipSystem>();
            _baseFields = owner.GetControllerComponent<ControllersBaseFields>();
        }

        public virtual void Attack()
        {
            if (_attackComponent.AttackProcess == null)
            {
                hitedList.Clear();
                _attackComponent.AttackProcess = mono.StartCoroutine(AttackProcess());
            }
        }

        public virtual void UnAttack()
        {
            _attackComponent.isAttackFrameThisFrame = false;
            _attackComponent.isAttackAnim = false;
            _spriteFlipSystem.IsActive = true;
            mono.StartCoroutine(Delay());
        }

        public IEnumerator Delay()
        {
            yield return new WaitForSeconds(0.1f);
            _attackComponent.AttackProcess = null;
            if (_healthComponent.currHealth <= 0)
            {
                ((Item)owner).DestroyItem();
            }
        }
        
        protected virtual IEnumerator AttackProcess()
        {
            yield return null;
                UnAttack(); 
        }
        public virtual void StopCoroutineSafely()
        {
            if (_attackComponent.AttackProcess == null)
            {
                mono.StopCoroutine(_attackComponent.AttackProcess);
                UnAttack();
            }
        }
    }
}