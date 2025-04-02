using System.Collections.Generic;
using Controllers;
using UnityEngine;

namespace Systems
{
    public class HealthUIController: UIController
    {
        public HealthUIData healthUIData = new HealthUIData();
        public HealthUISystem HealthUISystem = new HealthUISystem();

        protected override void InitSystems()
        {
            base.InitSystems();
            HealthUISystem.Initialize(this);
        }

        protected override void AddComponentsToList()
        {
            base.AddComponentsToList();
            AddControllerComponent(healthUIData);
        }

        protected override void AddSystemToList()
        {
            base.AddSystemToList();
            AddControllerSystem(HealthUISystem);
        }
    }

    [System.Serializable]
    public class HealthUIData : IComponent
    {
        public HealthUIItem Prefab;
        public EntityController entity;

        public List<HealthUIItem> healthes;
    }

    public class HealthUISystem : BaseSystem
    {
        private UIController _controller;
        private HealthUIData _uiData;
        private HealthComponent _healthComponent;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _controller = (UIController)base.owner;
            _uiData = _controller.GetControllerComponent<HealthUIData>();
            _healthComponent = _uiData.entity.healthComponent;
            int i = 0;
            int j = 0;
            while (_healthComponent.maxHealth > i)
            {
                HealthUIItem inst = Object.Instantiate(_uiData.Prefab, _controller.transform, true);
                inst.transform.localScale = Vector3.one;
                _uiData.healthes.Add(inst);
                inst.Init(_healthComponent,_uiData,j);
                j++;
                i += 5;
            }
        }

        public override void Update()
        {
            base.Update();
        }
    }
}