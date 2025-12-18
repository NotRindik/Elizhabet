using System.Collections;
using Controllers;
using Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Controllers
{
    public class InventoryUIController : UIController
    {
        public PlayerController playerController;
        private InventoryComponent _inventoryComponent;

        public ManaVisualComponent manaVisualComponent;
        private ManaVisualSystem _manaVisualSystem;
        private HolderSystem _holderSystem;

        public HolderComponent holderComponent = new HolderComponent();

        private ManaComponent manaComponent;

        protected void Start()
        {
            _inventoryComponent = playerController.GetControllerComponent<InventoryComponent>();
            manaComponent = playerController.GetControllerComponent<ManaComponent>();

            AddControllerComponent(_inventoryComponent);
            AddControllerComponent(manaComponent);


            _holderSystem = new HolderSystem();
            _holderSystem.Initialize(this);
            _manaVisualSystem = new ManaVisualSystem();
            _manaVisualSystem.Initialize(this);

        }
    }
   
}
namespace Systems
{
    using System;
    using UnityEngine.UI;

    public class ManaVisualSystem : BaseSystem, IDisposable
    {
        private ManaVisualComponent _manaVisual;
        private ManaComponent _manaComponent;

        public void Dispose()
        {
            _manaComponent.OnCurrManaDataChanged -= OnManaDataChange;

        }

        public override void Initialize(IController owner)
        {
            base.Initialize(owner);
            _manaVisual = owner.GetControllerComponent<ManaVisualComponent>();
            _manaComponent = owner.GetControllerComponent<ManaComponent>();
            _manaComponent.OnCurrManaDataChanged += OnManaDataChange;


            var slider = _manaVisual.manaSlider;

            slider.fillAmount = (float)_manaComponent.CurrMana / _manaComponent.MaxMana;
        }

        public void OnManaDataChange(float curr)
        {
            var slider = _manaVisual.manaSlider;
            slider.fillAmount = (float)_manaComponent.CurrMana / _manaComponent.MaxMana;
        }
    }

    [System.Serializable]
    public class ManaVisualComponent : IComponent
    {
        public Image manaSlider;
    }

    public class HolderSystem: BaseSystem
    {
        private HolderComponent _holderComponent;
        private Image sliderImageCache;
        private InventoryComponent _inventoryComponent;
        private Coroutine _durabilityFallProcess;
        public override void Initialize(IController owner)
        {
            base.Initialize(owner);
            _inventoryComponent = owner.GetControllerComponent<InventoryComponent>();
            _holderComponent = owner.GetControllerComponent<HolderComponent>();
            _inventoryComponent.OnActiveItemChange += Update;
            sliderImageCache = _holderComponent.durabilitySlider.fillRect.GetComponentInChildren<Image>();
            OnUpdate();
        }

        public void Update(Item activeItem, Item prevItem)
        {
            base.OnUpdate();
            if (prevItem)
            {
                var prevIndex = _inventoryComponent.items.Raw.FindIndex(prevStack =>
                {
                    if (prevStack != null)
                        return prevStack.itemName == prevItem.itemComponent.itemPrefab.name;
                    return false;
                }
                );
                var prevItemHealthComponent = prevItem.healthComponent;
                if (prevIndex != -1)
                {
                    _inventoryComponent.items[prevIndex].OnQuantityChange -= UpdateQuantityText;
                    if(prevItemHealthComponent != null)
                        prevItemHealthComponent.OnCurrHealthDataChanged -= UpdateDurabilitySlider; 
                }
            }

            if (activeItem == null)
            {
                _holderComponent.itemHolder.color = new Color(0, 0, 0, 0);
                sliderImageCache.color = new Color(0, 0, 0, 0);
                UpdateQuantityText(1);
                _holderComponent.durabilitySlider.value = _holderComponent.durabilitySlider.maxValue;
            }
            else
            {
                var activeHealth = activeItem.GetControllerComponent<HealthComponent>();
                if (activeHealth != null)
                {
                    _holderComponent.durabilitySlider.maxValue = activeHealth.maxHealth;
                    _holderComponent.durabilitySlider.value = activeHealth.currHealth;
                    activeHealth.OnCurrHealthDataChanged += UpdateDurabilitySlider;
                    UpdateDurabilitySlider(_inventoryComponent.CurrentActiveIndex > -1 ? activeHealth.currHealth : (int)_holderComponent.durabilitySlider.maxValue);
                    UpdateQuantityText(_inventoryComponent.CurrentActiveIndex > -1 ? _inventoryComponent.items[_inventoryComponent.CurrentActiveIndex].Count : 1);
                }
                _inventoryComponent.items[_inventoryComponent.CurrentActiveIndex].OnQuantityChange += UpdateQuantityText;
                _holderComponent.itemHolder.sprite = activeItem.itemComponent.itemIcon;
                _holderComponent.itemHolder.color = new Color(1, 1, 1, 1);
                _holderComponent.itemHolder.SetNativeSize();
            }
        }

        public void UpdateQuantityText(int quantity)
        {
            if (quantity > 1)
            {
                _holderComponent.itemQuantityText.text = quantity.ToString();   
            }
            else
            {
                _holderComponent.itemQuantityText.text = "";
            }
        }
        public void UpdateDurabilitySlider(float health)
        {
            if (_durabilityFallProcess != null)
            {
                mono.StopCoroutine(_durabilityFallProcess);
            }
            _durabilityFallProcess = mono.StartCoroutine(DurabilityDecreaseProcess(health));
        }
        public IEnumerator DurabilityDecreaseProcess(float health)
        {
            SliderColoringUpdate();
            while (!Mathf.Approximately(_holderComponent.durabilitySlider.value, health))
            {
                _holderComponent.durabilitySlider.value = Mathf.MoveTowards(_holderComponent.durabilitySlider.value,health,0.1f);
                SliderColoringUpdate();
                yield return new WaitForFixedUpdate();
            }
            
        }
        private void SliderColoringUpdate()
        {

            var percent = _holderComponent.durabilitySlider.value / _holderComponent.durabilitySlider.maxValue;
            if (percent < 0.8f)
            {
                sliderImageCache.color = new Color32(255, (byte)(255 * percent), 0, (byte)(120 * (1.3f - percent)));
            }
            else
            {
                sliderImageCache.color = new Color32(255, (byte)(255 * percent), 0, 0);
            }
        }
    }  
    
    
    [System.Serializable]
    public class HolderComponent: IComponent
    {
        public Image itemHolder;
        public TextMeshProUGUI itemQuantityText;
        public Slider durabilitySlider;
    }   
}
