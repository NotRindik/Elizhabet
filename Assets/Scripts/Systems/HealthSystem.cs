using System;
using System.Runtime.InteropServices;
using Controllers;
using UnityEngine;
namespace Systems
{
    public class HealthSystem: BaseSystem
    {
        private HealthComponent _healthComponent;
        private ArmourComponent armourComponent;
        public void TakeHit(HitInfo who)
        {
            _healthComponent.currHealth = Mathf.Max(_healthComponent.currHealth - who.dmg,0);
            _healthComponent.OnTakeHit?.Invoke(who);
            if (_healthComponent.currHealth <= 0)
            {
                _healthComponent.OnDie?.Invoke(owner);
            }
        }
        public void Heal(float heal)
        {
            _healthComponent.currHealth = Mathf.Min(_healthComponent.currHealth + heal, _healthComponent.maxHealth);
        }

        public void HealToMax() => Heal(_healthComponent.maxHealth);
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _healthComponent = base.owner.GetControllerComponent<HealthComponent>();
            armourComponent = base.owner.GetControllerComponent<ArmourComponent>();
            _healthComponent.currHealth = _healthComponent.maxHealth;
        }
    }

    public struct HitInfo
    {
        private Nullable<Vector2> hitPosition;   // если есть точка удара
        public Controller Attacker;    // если есть объект, кто нанёс урон
        public float dmg;

        public HitInfo(Vector2 pos)
        {
            hitPosition = pos;
            Attacker = null;
            this.dmg = 0;
        }
        public HitInfo(float dmg)
        {
            hitPosition = null;
            Attacker = null;
            this.dmg = dmg;
        }

        public HitInfo(Controller attacker)
        {
            Attacker = attacker;
            hitPosition = null;
            this.dmg = 0;
        }
        public HitInfo(Controller attacker,float dmg)
        {
            Attacker = attacker;
            hitPosition = null;
            this.dmg = dmg;
        }
        public HitInfo(Vector2 pos, float dmg)
        {
            hitPosition = pos;
            Attacker = null;
            this.dmg = dmg;
        }

        public HitInfo(Controller attacker, Vector2 pos)
        {
            Attacker = attacker;
            hitPosition = pos;
            this.dmg = 0;
        }

        public Vector2 GetHitPos()
        {
            if (hitPosition.HasValue)
                return hitPosition.Value;

            if (Attacker != null)
                return Attacker.transform.position;

            // fallback — если и точка, и атакер пустые
            return Vector2.zero;
        }

    }


    [System.Serializable]
    public class HealthComponent : IComponent
    {
        [SerializeField] private float _maxHealth;
        [SerializeField] private float _currHealth;

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
        public Action<Controller> OnDie;
        public Action<HitInfo> OnTakeHit;
    }

    public class Damage : IDamager
    {
        private DamageComponent _damageComponent;
        private ProtectionComponent _protectionComponent;
        public Damage(DamageComponent damageComponent, ProtectionComponent protectionComponent)
        {
            _damageComponent = damageComponent;
            _protectionComponent = protectionComponent;
        }
        public void ApplyDamage(HealthSystem hp, HitInfo who)
        {

            bool isCrit = UnityEngine.Random.value < _damageComponent.CritChance;
            float damage = isCrit ? _damageComponent.BaseDamage * _damageComponent.CritMultiplier
                                  : _damageComponent.BaseDamage;

            // 2. броня цели с учётом элемента
            float armor = 0;
            if (_protectionComponent != null)
            {
                armor = _protectionComponent.Protection;
            }
            float effectiveArmor = Mathf.Max(0, armor - _damageComponent.Penetration);

            // 3. формула снижения урона
            float finalDamage = Mathf.Max(1, damage - effectiveArmor / 2f);
            who.dmg = finalDamage;
            hp.TakeHit(who);
        }

        public float GetDamage()
        {
            return _damageComponent.BaseDamage;
        }

        public ElementType GetElementType()
        {
            return _damageComponent.Element;
        }
    }
/*
    public class AdditionalDamage : DamageUpgrade
    {
        private DamageComponent _damageComponent;
        private ProtectionComponent protectionComponent;

        public AdditionalDamage(IDamager damager, DamageComponent damageComponent, ProtectionComponent protectionComponent) : base(damager)
        {
            _damageComponent = damageComponent;
            this.protectionComponent = protectionComponent;
        }

        public override void ApplyDamage(HealthSystem hp, Vector2 who)
        {
            base.ApplyDamage(hp, who);

            bool isCrit = UnityEngine.Random.value < _damageComponent.CritChance;
            float damage = isCrit ? _damageComponent.BaseDamage * _damageComponent.CritMultiplier
                                  : _damageComponent.BaseDamage;

            // 2. броня цели с учётом элемента
            float armor = protectionComponent.Protection;
            float effectiveArmor = Mathf.Max(0, armor - _damageComponent.Penetration);

            // 3. формула снижения урона
            float finalDamage = damage * (100f / (100f + effectiveArmor));

            hp.TakeHit(finalDamage, who);
        }
    }

    public abstract class DamageUpgrade : IDamager
    {
        protected IDamager damager;

        public DamageUpgrade(IDamager damager)
        {
            this.damager = damager;
        }

        public virtual void ApplyDamage(HealthSystem hp, Vector2 who)
        {
            damager.ApplyDamage(hp, who);
        }

        public virtual float GetDamage()
        {
           return damager.GetDamage();
        }

        public virtual ElementType GetElementType()
        {
            return damager.GetElementType();
        }
    }
*/

    public interface IDamager
    {
        float GetDamage();
        ElementType GetElementType();

        void ApplyDamage(HealthSystem hp,HitInfo who);
    }


    [System.Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct DamageComponent : IComponent
    {
        public float BaseDamage;       // исходный урон
        public float CritChance;       // шанс крита
        public float CritMultiplier;   // множитель крита
        public float Penetration;      // пробивание брони
        public ElementType Element;    // стихия атаки

        public DamageComponent(float baseDamage, float critChance, float critMultiplier, float penetration
            , ElementType element)
        {
            BaseDamage = baseDamage;
            CritChance = critChance;
            CritMultiplier = critMultiplier;
            Penetration = penetration;
            Element = element;
        }
        public static DamageComponent operator+(DamageComponent damage1, DamageComponent damage2)
        {
            return new DamageComponent(damage1.BaseDamage + damage2.BaseDamage,damage1.CritChance + damage2.CritChance,
                damage1.CritMultiplier + damage2.CritMultiplier,
                damage1.Penetration + damage2.Penetration,ElementType.None);
        }

        public static DamageComponent operator *(DamageComponent damage1, DamageComponent damage2)
        {
            return new DamageComponent(damage1.BaseDamage * damage2.BaseDamage, damage1.CritChance * damage2.CritChance,
                damage1.CritMultiplier * damage2.CritMultiplier,
                damage1.Penetration * damage2.Penetration, ElementType.None);
        }

    }

    public enum ElementType
    {
        None,
        Physical,
        Fire,
        Water,
        Electro,
        Ice,
        Wind,
        Earth
    }

}