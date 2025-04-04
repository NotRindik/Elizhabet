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
        private HolderSystem _holderSystem = new HolderSystem();

        public HolderComponent holderComponent = new HolderComponent();

        protected void Start()
        {
            _inventoryComponent = playerController.GetControllerComponent<InventoryComponent>();
            Debug.Log(_inventoryComponent);
            AddControllerComponent(_inventoryComponent);
            _holderSystem.Initialize(this);
        }

        protected override void InitSystems()
        {
            base.InitSystems();
        }

        protected override void AddComponentsToList()
        {
            base.AddComponentsToList();
            AddControllerComponent(holderComponent);
        }
    
        protected override void AddSystemToList()
        {
            base.AddComponentsToList();
            AddControllerSystem(_holderSystem);
        }
    }
   
}
namespace Systems
{
    using UnityEngine.UI;
    public class HolderSystem: BaseSystem
    {
        private HolderComponent _holderComponent;
        private Image sliderImageCache;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            var inventoryComponent = owner.GetControllerComponent<InventoryComponent>();
            _holderComponent = owner.GetControllerComponent<HolderComponent>();
            inventoryComponent.OnActiveItemChange += Update;
            sliderImageCache = _holderComponent.durabilitySlider.fillRect.GetComponentInChildren<Image>();
            Update();
        }

        public void Update(Items activeItem, Items prevItem)
        {
            _holderComponent.durabilitySlider.maxValue = activeItem.itemComponent.maxDurability;
            if (prevItem != null)
            {
                prevItem.itemComponent.OnQuantityChange -= UpdateQuantityText;
                prevItem.itemComponent.OnDurabilityChange -= UpdateDurabilitySlider; 
            }
            UpdateQuantityText(activeItem.itemComponent.quantity);
            activeItem.itemComponent.OnQuantityChange += UpdateQuantityText;
            UpdateDurabilitySlider(activeItem.itemComponent.durability);
            activeItem.itemComponent.OnDurabilityChange += UpdateDurabilitySlider;
            if (activeItem == null)
            {
                _holderComponent.itemHolder.color = new Color(0, 0, 0, 0);
            }
            else
            {
                _holderComponent.itemHolder.sprite = activeItem.itemComponent.itemIcon;
                _holderComponent.itemHolder.color = new Color(1, 1, 1, 1);
            }
            base.Update();
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
        public void UpdateDurabilitySlider(int durability)
        {
            Debug.Log(durability);
            _holderComponent.durabilitySlider.value = durability;

            var percent = _holderComponent.durabilitySlider.value / _holderComponent.durabilitySlider.maxValue;
            if (percent < 0.5f)
            {
               sliderImageCache.color = new Color32(255, (byte)(255 * percent), 0, 120);
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
