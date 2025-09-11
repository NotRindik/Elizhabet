using Assets.Scripts.Systems;
using Controllers;
using System;
using UnityEngine;

namespace Systems {
    public class BerserkerModificator : BaseModificator, IDisposable
    {
        private BerserkerModificatorComponent _berserkerMod;
        private HealthComponent _health;
        private AttackComponent _attackComponent;

        public void Dispose()
        {
            _health.OnCurrHealthDataChanged -= OnHealthChange;
        }

        public override void Initialize(Controller owner)
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
                _attackComponent.damageModifire.Add(_berserkerMod.damageComponent);
            }
            else
            {
                if (_attackComponent.damageModifire.Raw.Contains(_berserkerMod.damageComponent))
                    _attackComponent.damageModifire.Remove(_berserkerMod.damageComponent);
            }
        }
    }

    public struct BerserkerModificatorComponent : IComponent
    {
        public DamageComponent damageComponent;

        public BerserkerModificatorComponent(DamageComponent damageComponent)
        {
            this.damageComponent = damageComponent;
        }
    }
}