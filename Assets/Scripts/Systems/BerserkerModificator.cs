using Assets.Scripts.Systems;
using System;
using Controllers;
using UnityEngine;
using UnityEngine.Serialization;

namespace Systems {
    
    [System.Serializable]
    public unsafe class BerserkerModificator : BaseModificator, IDisposable
    {
        private BerserkerModificatorComponent _berserkerMod;
        private HealthComponent _health;
        private InventoryComponent _inv;
        private AttackComponent _attackComponent;
        
        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            ref var berserkerModC = ref _modComponent.GetModBySystem(this).GetComponentByRef<BerserkerModificatorComponent>();
            
            berserkerModC.dmgPtr = std.Unsafe.MallocData(new DamageComponent());
            
            _health = owner.GetControllerComponent<HealthComponent>();
            _attackComponent = owner.GetControllerComponent<AttackComponent>();
            _inv = owner.GetControllerComponent<InventoryComponent>();
            
            _attackComponent.damageModifire.Add((IntPtr)berserkerModC.dmgPtr);
            
            _health.OnCurrHealthDataChanged += OnHealthChange;
            _inv.OnActiveStackChange += OnActiveItemChange;

            _berserkerMod = berserkerModC;
            OnHealthChange(_health.currHealth);
        }

        private void OnHealthChange(float hp)
        {
            RecalculateDamage();
        }
        
        private void OnActiveItemChange(ItemStack curr ,ItemStack prev)
        {
            RecalculateDamage(curr);
        }
        private void RecalculateDamage(ItemStack stack = null)
        {
            stack ??= _inv.ActiveStack;
            
            var weponC = stack?.GetItemComponentFromConfig<WeaponComponent>();
            
            if (stack == null || weponC == null)
            {
                *_berserkerMod.dmgPtr = default;
                return;
            }

            float healthPercent = _health.currHealth / _health.maxHealth;

            float t = Mathf.InverseLerp(1f, 0.3f, healthPercent);
            float bonusMultiplier = t;

            DamageComponent dmg = default;

            dmg.BaseDamage = weponC.damage.BaseDamage;

            dmg.BaseDamage *= bonusMultiplier;

            *_berserkerMod.dmgPtr = dmg;
        }
        
        public void Dispose()
        {
            ref var berserkerModC = ref _modComponent.GetModBySystem(this).GetComponentByRef<BerserkerModificatorComponent>();
            
            _attackComponent.damageModifire.Remove((IntPtr)berserkerModC.dmgPtr);
            
            _health.OnCurrHealthDataChanged -= OnHealthChange;
            _inv.OnActiveStackChange -= OnActiveItemChange;
            
            std.Unsafe.Free(berserkerModC.dmgPtr);
        }

    }

    [System.Serializable]
    public unsafe struct BerserkerModificatorComponent : IComponent
    {
        public DamageComponent* dmgPtr;
    }
}