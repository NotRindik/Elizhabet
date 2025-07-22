using System;
using Controllers;
using DefaultNamespace;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;

namespace Systems
{
    public class HealthSystem: BaseSystem
    {
        private HealthComponent _healthComponent;
        public void TakeHit(float damage, [CanBeNull] Vector2 who)
        {
            _healthComponent.currHealth -= damage;
            _healthComponent?.OnTakeHit(damage, who);
            if (_healthComponent.currHealth <= 0)
            {
                Debug.Log("DIE");
                GameObject.Destroy(owner.gameObject);
            }
        }

        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _healthComponent = base.owner.GetControllerComponent<HealthComponent>();
            _healthComponent.currHealth = _healthComponent.maxHealth;
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
        public Action<float, Vector2> OnTakeHit;
    }
}