using Assets.Scripts.Systems;
using Controllers;
using System;
using Unity.Collections.LowLevel;
using Unity.Collections.LowLevel.Unsafe;

namespace Systems {
    
    public unsafe class BerserkerModificator : BaseModificator, IDisposable
    {
        private BerserkerModificatorComponent _berserkerMod;
        private HealthComponent _health;
        private AttackComponent _attackComponent;

        public void Dispose()
        {
            _health.OnCurrHealthDataChanged -= OnHealthChange;
            UnsafeUtility.Free(_berserkerMod.damageComponent,Unity.Collections.Allocator.Persistent);
        }

        public override void Initialize(IController owner)
        {
            base.Initialize(owner);
            _berserkerMod = _modComponent.GetModComponent<BerserkerModificatorComponent>();
            _health = owner.GetControllerComponent<HealthComponent>();
            _attackComponent = owner.GetControllerComponent<AttackComponent>();

            _health.OnCurrHealthDataChanged += OnHealthChange;
        }

        private void OnHealthChange(float hp)
        {
            if(hp/_health.maxHealth < 0.3f)
            {
                _attackComponent.damageModifire.Add((IntPtr)_berserkerMod.damageComponent);
            }
            else
            {
                if (_attackComponent.damageModifire.Raw.Contains((IntPtr)_berserkerMod.damageComponent))
                    _attackComponent.damageModifire.Remove((IntPtr)_berserkerMod.damageComponent);
            }
        }
    }

    public unsafe struct BerserkerModificatorComponent : IComponent
    {
        public DamageComponent* damageComponent;

        public BerserkerModificatorComponent(DamageComponent* damageComponent)
        {
            this.damageComponent = damageComponent;
        }
    }
}