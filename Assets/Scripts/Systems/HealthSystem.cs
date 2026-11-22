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

            var wasAlive = _healthComponent.currHealth > 0;
            _healthComponent.currHealth = Mathf.Max(_healthComponent.currHealth - who.finalDmg,0);
            _healthComponent.OnTakeHit?.Invoke(who);
            _healthComponent.OnTakeHitSer?.Invoke();

            EventBus.OnDamageApplied?.Invoke(who);
            if (wasAlive && _healthComponent.currHealth <= 0)
            {
                _healthComponent.OnDie?.Invoke(owner);
                _healthComponent.OnDieSerialized?.Invoke();
            }
        }

        public void SetHealth(float health,HitInfo info = default)
        {
            var wasAlive = _healthComponent.currHealth > 0;
            _healthComponent.currHealth = health;
            _healthComponent.OnTakeHit?.Invoke(info);
            _healthComponent.OnTakeHitSer?.Invoke();

            if (wasAlive && _healthComponent.currHealth <= 0)
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
                if(_bodyType == null && Target.mono.TryGetComponent<TagManager>(out var tags))
                    _bodyType = tags.GetTag<BodyTypeTag>().bodyType;
                return _bodyType;
            }
        }
        
        public float finalDmg;
        public bool IsCrit;
        public bool dontPlayHitAnim;

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

    public interface ISaveSerialize
    {
        void ApplyConfig(AbstractEntity config);
    }
    
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

        public bool IsDie => currHealth <= 0;
        
        public Action<float> OnCurrHealthDataChanged;
        public Action<float> OnMaxHealthDataChanged;
        public Action<AbstractEntity> OnDie;
        public Action<HitInfo> OnTakeHit;

        public UnityEvent OnDieSerialized;
        public UnityEvent OnTakeHitSer;
        public void ApplyConfig(AbstractEntity config)
        {
        }
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
            
            
            float effectiveArmor = CalculateProtection(ref who);

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
            if (who.WeaponType == null || who.WeaponType.piercingDatas == null)
            {
                data = null;
                return false;
            }
            data = who.WeaponType.piercingDatas.FirstOrDefault(data => data.bodyType == bodyType);
            return data != null;
        }

        public float GetDamage()
        {
            return _damageComponent.BaseDamage;
        }
    }
    
    public struct EnemyDamage : IDamager
    {
        private DamageComponent _damageComponent;
        public EnemyDamage(DamageComponent damageComponent)
        {
            _damageComponent = damageComponent;
        }
        public void ApplyDamage(HealthSystem hp, ref HitInfo who)
        {
            float damage = _damageComponent.BaseDamage;
            
            float effectiveArmor = CalculateProtection(ref who);

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
        
        public static DamageComponent Lerp(DamageComponent min, DamageComponent max, float t)
        {
            return new DamageComponent(
                Mathf.Lerp(min.BaseDamage, max.BaseDamage, t),
                Mathf.Lerp(min.CritChance, max.CritChance, t),
                Mathf.Lerp(min.CritMultiplier, max.CritMultiplier, t),
                Mathf.Lerp(min.Penetration, max.Penetration, t));
        }
    }
}