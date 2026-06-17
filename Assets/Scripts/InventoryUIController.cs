using DG.Tweening;
using Systems;
using TMPro;
using UnityEngine;

namespace Controllers
{
    public class InventoryUIController : UIController
    {
        public PlayerController playerController => ContextManager.Instance.player;
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

        public override void Initialize(AbstractEntity owner)
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

    public class HolderSystem : BaseSystem, IDisposable
{
    private HolderComponent _holderComponent;
    private InventoryComponent _inventoryComponent;
    private Image _sliderImageCache;
    private Tween _durabilityTween;
    private const float DURABILITY_TWEEN_TIME = 0.25f;
    
    private ItemStack _prevStack;

    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        _inventoryComponent = owner.GetControllerComponent<InventoryComponent>();
        _holderComponent    = owner.GetControllerComponent<HolderComponent>();
        _sliderImageCache   = _holderComponent.durabilitySlider.fillRect.GetComponentInChildren<Image>();

        _inventoryComponent.OnActiveItemChange += OnActiveItemChange;
        OnActiveItemChange(_inventoryComponent.ActiveItem, null);
    }

    private void OnActiveItemChange(Item activeItem, Item prevItem)
    {
        
        if (_prevStack != null)
            _prevStack.OnQuantityChange -= UpdateQuantityText;
        if (prevItem != null)
            prevItem.healthComponent.OnCurrHealthDataChanged -= UpdateDurabilitySlider;

        if (activeItem == null)
        {
            _holderComponent.itemHolder.color = new Color(0, 0, 0, 0);
            _sliderImageCache.color           = new Color(0, 0, 0, 0);
            UpdateQuantityText(0);
            _holderComponent.durabilitySlider.value = _holderComponent.durabilitySlider.maxValue;
            return;
        }

        // Иконка
        _holderComponent.itemHolder.sprite = activeItem.itemComponent.itemIcon;
        _holderComponent.itemHolder.color  = Color.white;
        _holderComponent.itemHolder.SetNativeSize();

        // Durability
        var health = activeItem.healthComponent;
        if (health != null)
        {
            _holderComponent.durabilitySlider.maxValue = health.maxHealth;
            _holderComponent.durabilitySlider.value    = health.currHealth;
            health.OnCurrHealthDataChanged += UpdateDurabilitySlider;
            SliderColoringUpdate();
        }

        // Quantity — берём из активного стака
        var activeStack = _inventoryComponent.ActiveStack;
        if (activeStack != null)
        {
            activeStack.OnQuantityChange += UpdateQuantityText;
            UpdateQuantityText(activeStack.Count);
        }
        
        _prevStack = _inventoryComponent.ActiveStack;
        if (_prevStack != null)
            _prevStack.OnQuantityChange += UpdateQuantityText;
    }

    public void UpdateQuantityText(int quantity)
    {
        _holderComponent.itemQuantityText.text = quantity > 1 ? quantity.ToString() : "";
    }

    public void UpdateDurabilitySlider(float health)
    {
        _durabilityTween?.Kill();
        _durabilityTween = DOTween.To(
            () => _holderComponent.durabilitySlider.value,
            x  => { _holderComponent.durabilitySlider.value = x; SliderColoringUpdate(); },
            health,
            DURABILITY_TWEEN_TIME
        ).SetEase(Ease.OutQuad);
    }

    private void SliderColoringUpdate()
    {
        float percent = _holderComponent.durabilitySlider.value
                      / _holderComponent.durabilitySlider.maxValue;

        _sliderImageCache.color = percent < 0.8f
            ? new Color32(255, (byte)(255 * percent), 0, (byte)(120 * (1.3f - percent)))
            : new Color32(255, (byte)(255 * percent), 0, 0);
    }

    public void Dispose()
    {
        if (_inventoryComponent == null) return;
        _inventoryComponent.OnActiveItemChange -= OnActiveItemChange;

        // Чистим подписки активного предмета если есть
        if (_inventoryComponent.ActiveItem != null)
            _inventoryComponent.ActiveItem.healthComponent.OnCurrHealthDataChanged -= UpdateDurabilitySlider;

        var activeStack = _inventoryComponent.ActiveStack;
        if (activeStack != null)
            activeStack.OnQuantityChange -= UpdateQuantityText;
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
