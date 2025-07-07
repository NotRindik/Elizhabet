using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts;
using Systems;
using UnityEngine;

namespace Controllers
{
    public class MeleeWeapon : Weapon
    {
        public MeleeComponent meleeComponent = new MeleeComponent();
        public MeleeWeaponSystem meleeWeaponSystem;
        public override void SelectItem(Controller owner)
        {
            base.SelectItem(owner);
            meleeWeaponSystem = new MeleeWeaponSystem();
            meleeWeaponSystem.Initialize(this);
            AddControllerSystem(meleeWeaponSystem);
            nonInitComponents.Add(typeof(MeleeComponent));
        }
        public override void InitAfterSpawnFromInventory(Dictionary<Type, IComponent> invComponents)
        {
            nonInitComponents.Add(typeof(MeleeComponent));
            base.InitAfterSpawnFromInventory(invComponents);
        }
    }

    [System.Serializable]
    public class MeleeComponent : IComponent
    {
        public float attackSpeed;
        public float pushbackForce = 10f;
        public float liftForce = 3f; 
        
        public TrailRenderer trail;
        
        public PolygonCollider2D polygonCollider;
        public List<Vector2> points = new List<Vector2>();
        
        public Collider2D[] CheckObjectsInsideTrail(out int hitCount,LayerMask layerMask)
        {
            Collider2D[] hits = new Collider2D[10];
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(layerMask);
                
            hitCount = polygonCollider.Overlap(filter, hits);

            return hits;
        }
    }
    
    public class MeleeWeaponSystem: BaseSystem
    {
        protected List<GameObject> hitedList = new List<GameObject>();
        protected WeaponComponent _weaponComponent;

        protected MeleeComponent _meleeComponent;
        protected AttackComponent _attackComponent;
        protected HealthComponent _healthComponent;

        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _meleeComponent = base.owner.GetControllerComponent<MeleeComponent>();
            _attackComponent = base.owner.GetControllerComponent<AttackComponent>();
            _healthComponent = base.owner.GetControllerComponent<HealthComponent>();
            _weaponComponent = base.owner.GetControllerComponent<WeaponComponent>();
        }

        public virtual void Attack()
        {
            hitedList.Clear();
            if (_attackComponent.AttackProcess == null)
            {
                _attackComponent.AttackProcess = owner.StartCoroutine(AttackProcess());
            }
        }

        public virtual void UnAttack()
        {
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
    }
}