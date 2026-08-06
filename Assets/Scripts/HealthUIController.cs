using System;
using System.Collections.Generic;
using Controllers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Systems
{
    [DefaultExecutionOrder(10)]
    public class HealthUIController : UIController
    {
        public HealthUIData healthUIData = new HealthUIData();
        public HealthUISystem HealthUISystem = new HealthUISystem();
    }

    [System.Serializable]
    public class HealthUIData : IComponent
    {
        public HealthUIItem Prefab;

        public List<HealthUIItem> healthes;
    }
    public class HealthUISystem : BaseSystem,IDisposable    
    {
        private UIController _controller;
        private HealthUIData _uiData;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _controller = (UIController)base.owner;
            _uiData = _controller.GetControllerComponent<HealthUIData>();
            EventBus.OnPlayerChange += OnPlayerChange;
            
            var player = ContextManager.Instance.player;
            
            if (player != null)
            {
                OnPlayerChange(player);
            }
        }

        public void ClearHearts()
        {
            for (int i = 0; i < _uiData.healthes.Count; i++)
            {
                Object.Destroy(_uiData.healthes[i].gameObject);
            }
            _uiData.healthes.Clear();
        }

        public void RespawnHearts(PlayerController player)
        {
            ClearHearts();
            Debug.Log("Heart Respawned");
            int i = 0;
            int j = 0;
            var healthComponent = player.GetControllerComponent<HealthComponent>();
            while (healthComponent.maxHealth > i)
            {
                HealthUIItem inst = Object.Instantiate(_uiData.Prefab, _controller.transform, true);
                inst.transform.localScale = Vector3.one;
                _uiData.healthes.Add(inst);
                inst.Init(healthComponent,_uiData,j);
                j++;
                i += 5;
            }
        }

        public void OnPlayerChange(PlayerController player)
        {
            RespawnHearts(player);
        }
        
        public void Dispose()
        {
            EventBus.OnPlayerChange -= OnPlayerChange;
        }
    }
}