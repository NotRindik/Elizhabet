using System;
using Controllers;
using DefaultNamespace;
using UnityEngine;

namespace Systems
{
    public class HealthSystem: BaseSystem,ITakeHit
    {
        public Stats stats;
        private HealthComponent _healthComponent;
        public void TakeHit(float damage)
        {
            stats["health"] = (float)stats["health"] - damage;
            Debug.Log(stats["health"]);
            if ((float)stats["health"] <= 0)
            {
                Debug.Log("DIE");
                GameObject.Destroy(owner.gameObject);
            }
        }

        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _healthComponent = base.owner.GetControllerComponent<HealthComponent>();
            stats = base.owner.GetControllerComponent<Stats>();
            stats["health"] = _healthComponent.startHealth;
            Debug.Log(stats["health"]);
        }
    }
    
    [System.Serializable]
    public class HealthComponent : IComponent
    {
        public float startHealth;
    }
}