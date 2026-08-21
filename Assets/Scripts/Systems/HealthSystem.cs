using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

namespace Systems
{
    [System.Serializable]
    public class HealthSystem: BaseSystem,IDisposable
    {
        private HealthComponent _healthComponent;
        
        public void TakeHit(in HitInfo who)
        {
            if(!IsActive)
                return;
            
            _healthComponent.currHealth = Mathf.Max(_healthComponent.currHealth - who.finalDmg,0);
            _healthComponent.OnTakeHit?.Invoke(who);
            _healthComponent.OnTakeHitSer?.Invoke();
            
            EventBus.OnDamageApplied?.Invoke(who);
            if (_healthComponent.currHealth <= 0)
            {
                _healthComponent.OnDie?.Invoke(owner);
                _healthComponent.OnDieSerialized?.Invoke();
            }
        }

        public void SetHealth(float health)
        {
            _healthComponent.currHealth = health;
            _healthComponent.OnTakeHit?.Invoke(new HitInfo());
            _healthComponent.OnTakeHitSer?.Invoke();
            
            if (_healthComponent.currHealth <= 0)
            {
                _healthComponent.OnDie?.Invoke(owner);
                _healthComponent.OnDieSerialized?.Invoke();
            }
        }
        public void Heal(float heal)
        {
            _healthComponent.currHealth = Mathf.Min(_healthComponent.currHealth + heal, _healthComponent.maxHealth);
        }

        public void HealToMax() => Heal(_healthComponent.maxHealth);
        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _healthComponent = base.owner.GetControllerComponent<HealthComponent>();
            _healthComponent.currHealth = _healthComponent.maxHealth;
        }
        public void Dispose()
        {
            _healthComponent.OnDie = null;
            _healthComponent.OnCurrHealthDataChanged = null;
            _healthComponent.OnMaxHealthDataChanged = null;
            _healthComponent.OnTakeHit = null;
            //_healthComponent.OnDieSerialized.RemoveAllListeners();
            //_healthComponent.OnTakeHitSer.RemoveAllListeners();
        }
    }

    public struct HitInfo
    {
        public Nullable<Vector2> hitPosition;
        public AbstractEntity Attacker,Target;
        public WeaponType WeaponType;
        public BodyType _bodyType;
        public BodyType BodyType
        {
            get
            {
                if(_bodyType == null)
                    _bodyType = Target.mono.GetComponent<TagManager>()?.GetTag<BodyTypeTag>().bodyType;
                return _bodyType;
            }
        }
        
        public float finalDmg;
        public bool IsCrit;

        public Vector2 AttackVelocity;

        public Vector2 GetHitPos()
        {
            if (hitPosition.HasValue)
                return hitPosition.Value;

            if (Attacker != null)
                return Attacker.mono.transform.position;
            return Vector2.zero;
        }
    }

    public interface ISaveSerialize{}
    
    [System.Serializable]
    public class HealthComponent : IComponent, ISaveSerialize
    {
        [SaveField] [SerializeField] private float _maxHealth;
        [SaveField] [SerializeField] private float _currHealth;

        public float maxHealth
        {
            get => _maxHealth;
            set
            {
                _maxHealth = value;
                OnMaxHealthDataChanged?.Invoke(_maxHealth);
            }
        }

        public float currHealth
        {
            get => _currHealth;
            set
            {
                _currHealth = value;
                OnCurrHealthDataChanged?.Invoke(_currHealth);
            }
        }
        
        public Action<float> OnCurrHealthDataChanged;
        public Action<float> OnMaxHealthDataChanged;
        public Action<AbstractEntity> OnDie;
        public Action<HitInfo> OnTakeHit;

        public UnityEvent OnDieSerialized;
        public UnityEvent OnTakeHitSer;
    }

    public unsafe struct Damage : IDamager
    {
        private DamageComponent _damageComponent;
        public Damage(DamageComponent damageComponent)
        {
            _damageComponent = damageComponent;
        }
        public void ApplyDamage(HealthSystem hp, ref HitInfo who)
        {
            float damage = _damageComponent.BaseDamage;

            CalculateCrit(&damage, ref who);

            if (BodyTypeContains(ref who, out var data))
            {
                damage *= data.damageMultiplier;
            }
            
            
            float effectiveArmor = Mathf.Max(0, CalculateProtection(ref who) - _damageComponent.Penetration);

            float finalDamage = Mathf.Max(1, damage - effectiveArmor / 2f);
            
            who.finalDmg = finalDamage;
            
            hp.TakeHit(who);
        }

        public float CalculateProtection(ref HitInfo who)
        {
            float armor = 0;
            
            ProtectionComponent protectionComponent = who.Target.GetControllerComponent<ProtectionComponent>();
            
            if (protectionComponent != null)
            {
                armor = protectionComponent.Protection;
            }
            
            return Mathf.Max(0, armor - _damageComponent.Penetration);
        }

        public void CalculateCrit(float* damage,ref HitInfo who)
        {
            var weaponType = who.WeaponType;
            
            
            if (weaponType != null && BodyTypeContains(ref who, out var data) && data.damageMultiplier > 1)
            {
                bool isCrit = Random.value < _damageComponent.CritChance;

                if(isCrit)
                    *damage = _damageComponent.BaseDamage * _damageComponent.CritMultiplier;

                who.IsCrit = isCrit;
            }
        }

        public bool BodyTypeContains(ref HitInfo who,out PiercingData data)
        {
            var bodyType = who.BodyType;
            data = who.WeaponType.piercingDatas.FirstOrDefault(data => data.bodyType == bodyType);
            return data != null;
        }

        public float GetDamage()
        {
            return _damageComponent.BaseDamage;
        }
    }

    public interface IDamager
    {
        float GetDamage();

        void ApplyDamage(HealthSystem hp,ref HitInfo who);
    }


    [System.Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct DamageComponent : IComponent
    {
        public float BaseDamage;
        public float CritChance;
        public float CritMultiplier;
        public float Penetration;

        public DamageComponent(float baseDamage, float critChance, float critMultiplier, float penetration)
        {
            BaseDamage = baseDamage;
            CritChance = critChance;
            CritMultiplier = critMultiplier;
            Penetration = penetration;
        }
        public static DamageComponent operator+(DamageComponent damage1, DamageComponent damage2)
        {
            return new DamageComponent(damage1.BaseDamage + damage2.BaseDamage,damage1.CritChance + damage2.CritChance,
                damage1.CritMultiplier + damage2.CritMultiplier,
                damage1.Penetration + damage2.Penetration);
        }

        public static DamageComponent operator *(DamageComponent damage1, DamageComponent damage2)
        {
            return new DamageComponent(damage1.BaseDamage * damage2.BaseDamage, damage1.CritChance * damage2.CritChance,
                damage1.CritMultiplier * damage2.CritMultiplier,
                damage1.Penetration * damage2.Penetration);
        }

    }
}