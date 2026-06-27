using System.Collections;
using Controllers;
using DG.Tweening;
using Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    public class HolderSystem: BaseSystem,IDisposable
    {
        private HolderComponent _holderComponent;
        private Image sliderImageCache;
        private InventoryComponent _inventoryComponent;
        private Coroutine _durabilityFallProcess;
        private const float DURABILITY_TWEEN_TIME = 0.25f;
        private Tween _durabilityTween;
        private Tween _iconTween;
        private HealthComponent _currentHealth;
        private ItemStack _currentStack;
        
        
        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _inventoryComponent = owner.GetControllerComponent<InventoryComponent>();
            _holderComponent = owner.GetControllerComponent<HolderComponent>();
            _inventoryComponent.OnActiveStackChange += Update;
            sliderImageCache = _holderComponent.durabilitySlider.fillRect.GetComponentInChildren<Image>();
            OnUpdate();
        }
        
        
        public void Update(ItemStack activeItem, ItemStack prevItem)
        {
            if (_holderComponent == null || sliderImageCache == null)
                return;

            if (prevItem != null && prevItem.Count > 0)
            {
                prevItem.OnQuantityChange -= UpdateQuantityText;

                var prevHealth = prevItem.GetItemComponent<HealthComponent>();

                if (prevHealth != null)
                    prevHealth.OnCurrHealthDataChanged -= UpdateDurabilitySlider;
            }

            if (activeItem == null)
            {
                HideHolder();
                return;
            }

            _currentStack = activeItem;
            _currentHealth = activeItem.GetItemComponent<HealthComponent>();

            activeItem.OnQuantityChange += UpdateQuantityText;
            UpdateQuantityText(activeItem.Count);

            if (_currentHealth != null)
            {
                _currentHealth.OnCurrHealthDataChanged += UpdateDurabilitySlider;
                _holderComponent.durabilitySlider.maxValue = _currentHealth.maxHealth;
                UpdateDurabilitySliderImmediate(_currentHealth.currHealth);
            }

            ChangeItem(activeItem.GetItemComponent<ItemComponent>().itemIcon);
        }

        
        private void ChangeItem(Sprite icon)
        {
            _iconTween?.Kill();

            Sequence seq = DOTween.Sequence();

            seq.Append(
                _holderComponent.itemHolder.DOFade(0f, 0.08f)
            );

            seq.Join(
                _holderComponent.itemHolder.transform
                    .DOScale(0.8f, 0.08f)
            );

            seq.AppendCallback(() =>
            {
                _holderComponent.itemHolder.sprite = icon;
                _holderComponent.itemHolder.SetNativeSize();
            });

            seq.Append(
                _holderComponent.itemHolder.DOFade(1f, 0.12f)
            );

            seq.Join(
                _holderComponent.itemHolder.transform
                    .DOScale(1f, 0.12f)
                    .SetEase(Ease.OutBack)
            );
            

            _iconTween = seq;
        }
        private void HideHolder()
        {
            _iconTween?.Kill();

            Sequence seq = DOTween.Sequence();

            seq.Append(
                _holderComponent.itemHolder.DOFade(0f, 0.15f)
            );

            seq.Join(
                _holderComponent.itemHolder.transform
                    .DOScale(0.8f, 0.15f)
            );

            sliderImageCache.DOFade(0f, 0.15f);

            _holderComponent.itemQuantityText.text = "";

            _iconTween = seq;
        }
        
        public void UpdateQuantityText(int quantity)
        {
            _holderComponent.itemQuantityText.text =
                quantity > 1 ? quantity.ToString() : "";

            _holderComponent.itemQuantityText.transform.DOKill();

            _holderComponent.itemQuantityText.transform.localScale = Vector3.one;

            _holderComponent.itemQuantityText.transform
                .DOPunchScale(Vector3.one * 0.15f, 0.15f);
        }

        public void UpdateDurabilitySliderImmediate(float health)
        {
            _holderComponent.durabilitySlider.value = health;
            SliderColoringUpdate();
        }
        
        public void UpdateDurabilitySlider(float health)
        {
            _durabilityTween?.Kill();
            _durabilityTween = DOTween.To(
                    () => _holderComponent.durabilitySlider.value,
                    x =>
                    {
                        _holderComponent.durabilitySlider.value = x;
                        SliderColoringUpdate();
                    },
                    health,
                    DURABILITY_TWEEN_TIME
                )
                .SetEase(Ease.OutQuad);
        }
        
        private void SliderColoringUpdate()
        {
            float percent = _holderComponent.durabilitySlider.value /
                            _holderComponent.durabilitySlider.maxValue;

            if (percent < 0.8f)
            {
                sliderImageCache.color = new Color32(
                    255,
                    (byte)(255 * percent),
                    0,
                    (byte)(120 * (1.3f - percent))
                );
                
                if (percent < 0.15f)
                {
                    _holderComponent.durabilitySlider.transform.DOKill();

                    _holderComponent.durabilitySlider.transform
                        .DOPunchScale(Vector3.one * 0.05f, 0.15f);
                }
            }
            else
            {
                sliderImageCache.color = new Color32(255, (byte)(255 * percent), 0, 0);
            }
        }
        public void Dispose()
        {
            if(_inventoryComponent != null)
                _inventoryComponent.OnActiveStackChange -= Update;
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
