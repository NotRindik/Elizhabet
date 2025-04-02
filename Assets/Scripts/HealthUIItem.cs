using UnityEngine;
using UnityEngine.UI;
using Systems;

namespace Controllers
{
    public class HealthUIItem : UIController
    {
        [SerializeField] public Image image;
        

        private HealthComponent _healthComponent;
        private HealthItemUIComponent itemComponent = new HealthItemUIComponent();
        private HealthUIData _uiData;
        

        public void Init(HealthComponent healthComponent,HealthUIData uiData,int itemIndex)
        {
            _healthComponent = healthComponent;
            _uiData = uiData;
            itemComponent.ItemIndex = itemIndex;
            _healthComponent.OnCurrHealthDataChanged += OnUpdate;
            OnUpdate(_healthComponent.currHealth);
        }

        public void OnUpdate(float health)
        {
            float cellHP = Mathf.Clamp(health - ((_uiData.healthes.Count - itemComponent.ItemIndex-1) * 5), 0, 5);
            image.fillAmount = cellHP / 5;
        }
    }
}

namespace Systems
{
    public class HealthItemUIComponent : IComponent
    {
        public float MaxHealth = 5;
        public float CurrHealth = 5;
        public int ItemIndex = 0;
    }    
}
