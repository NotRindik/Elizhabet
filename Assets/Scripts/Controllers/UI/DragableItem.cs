using System;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using Controllers;
using DG.Tweening;
using Sirenix.OdinInspector;
using Systems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragableItem : SerializedMonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    private InventoryItemData _itemData;
    private HealthComponent _healthComponent;

    public InventoryItemData itemData
    {
        get => _itemData;
        set
        {
            _itemData = value;
            UpdateQuantity(1);
            ApplyStickerVisuals();
        }
    }

    [TabGroup("References", "UI")]
    [SerializeField] private Slider slider;
    [TabGroup("References", "UI")]
    [SerializeField] private Image sliderfill;
    [TabGroup("References", "UI")]
    [SerializeField] private TextMeshProUGUI tmPro;
    [TabGroup("References", "UI")]
    public Image image;

    [TabGroup("References", "Canvas Groups")]
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [TabGroup("References", "Canvas Groups")]
    [SerializeField] private CanvasGroup stickerCanvasGroup;
    [TabGroup("References", "Canvas Groups")]
    [SerializeField] private Image stickerBackgroundImage;

    [TabGroup("Drag", "Follow")]
    [SerializeField] private float followSmoothTime = 0.08f;

    [TabGroup("Drag", "Context")]
    [SerializeField] private Vector3 beltScale = Vector3.one * 1.3f;
    [TabGroup("Drag", "Context")]
    [SerializeField] private Vector3 bookScale = Vector3.one;
    [TabGroup("Drag", "Context")]
    [SerializeField] private float contextTweenDuration = 0.15f;
    [TabGroup("Drag", "Context")]
    [SerializeField] private LayerMask uiRaycastMask;

    [TabGroup("Sticker", "Color")]
    [DictionaryDrawerSettings(KeyLabel = "Type", ValueLabel = "Color")]
    [SerializeField] private Dictionary<IInventoryFilter.FilterType, Color> stickerColorsByType;
    [TabGroup("Sticker", "Color")]
    [SerializeField] private Color defaultStickerColor = Color.white;
    [TabGroup("Sticker", "Sprites")]
    [SerializeField] private Sprite[] stickerSpriteVariations;

    [TabGroup("Sticker", "Rotation")]
    [MinMaxSlider(-90, 90)]
    [SerializeField] private Vector2 stickerRotationRange = new Vector2(-8f, 8f);

    [HideInInspector] public int currPage;
    [HideInInspector] public int slotIndex;
    [HideInInspector] public SlotBase sourceSlot;
    [HideInInspector, NonSerialized] public Action OnClick;

    private Transform _parentAfterDrag;
    public Transform parentAfterDrag
    {
        get => _parentAfterDrag;
        set
        {
            if (_parentAfterDrag != value)
            {
                _parentAfterDrag = value;
                StartDragAnimation();
            }
        }
    }

    private RectTransform _rectTransform;
    private Canvas _canvas;

    private Tween dragAnim;
    private Tween _scaleTween;
    private Tween _stickerFadeTween;
    private Tween _rotationTween;

    private Vector3 _dragTargetPosition;
    private Vector3 _followVelocity;
    private bool _isDragging;
    private bool? _lastBeltState;
    private Sprite _cachedSticker;
    private bool _stickerPicked;
    
    private readonly List<RaycastResult> _raycastBuffer = new();

    private void Start()
    {
        name = itemData.Item.itemName;
        itemData.Item.OnQuantityChange += UpdateQuantity;

        _rectTransform = transform as RectTransform;
        _canvas = GetComponentInParent<Canvas>();
    }

    private void ApplyStickerVisuals()
    {
        if (stickerBackgroundImage == null || _itemData?.Item == null)
            return;

        var type = ResolveItemType(_itemData.Item);
        stickerBackgroundImage.color = stickerColorsByType != null && stickerColorsByType.TryGetValue(type, out var color)
            ? color
            : defaultStickerColor;

        if (!_stickerPicked)
        {
            _cachedSticker = PickRandomSticker();
            _stickerPicked = true;
        }

        stickerBackgroundImage.sprite = _cachedSticker;
    }

    private Sprite PickRandomSticker()
    {
        if (stickerSpriteVariations == null || stickerSpriteVariations.Length == 0)
            return null;

        return stickerSpriteVariations[UnityEngine.Random.Range(0, stickerSpriteVariations.Length)];
    }

    private IInventoryFilter.FilterType ResolveItemType(ItemStack stack)
    {
        if (stack.GetItemComponent<ArmourItemComponent>() != null)
            return IInventoryFilter.FilterType.Armours;
        if (stack.GetItemComponent<WeaponComponent>() != null)
            return IInventoryFilter.FilterType.Weapons;

        return IInventoryFilter.FilterType.None;
    }

    public void SetVisualContext(bool isBelt)
    {
        _lastBeltState = isBelt;

        _scaleTween?.Kill();
        _scaleTween = transform.DOScale(isBelt ? beltScale : bookScale, contextTweenDuration);

        _stickerFadeTween?.Kill();
        _stickerFadeTween = stickerCanvasGroup.DOFade(isBelt ? 0f : 1f, contextTweenDuration);

        RandomizeStickerRotation(isBelt);
    }

    public void RandomizeStickerRotation(bool isBelt)
    {
        float angle = isBelt ? 0f : UnityEngine.Random.Range(stickerRotationRange.x, stickerRotationRange.y);

        _rotationTween?.Kill();
        _rotationTween = transform.DOLocalRotate(new Vector3(0f, 0f, angle), contextTweenDuration);
    }

    private void Update()
    {
        if (!_isDragging) return;

        _rectTransform.position = Vector3.SmoothDamp(
            _rectTransform.position,
            _dragTargetPosition,
            ref _followVelocity,
            followSmoothTime
        );
    }

    public void UpdateQuantity(int quantity)
    {
        if (_healthComponent == null)
        {
            _healthComponent = itemData.Item.GetItemComponent<HealthComponent>();
            _healthComponent.OnCurrHealthDataChanged += UpdateSlider;
        }

        tmPro.text = quantity > 1 ? $"{quantity}" : string.Empty;
    }

    public void UpdateSlider(float health)
    {
        if (_healthComponent == null)
            _healthComponent = itemData.Item.GetItemComponent<HealthComponent>();

        slider.maxValue = _healthComponent.maxHealth;
        slider.value = health;
        var percent = slider.value / slider.maxValue;

        sliderfill.color = percent < 0.8f
            ? new Color32(255, (byte)(255 * percent), 0, (byte)(120 * (1.3f - percent)))
            : new Color32(255, (byte)(255 * percent), 0, 0);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        parentAfterDrag = transform.parent;
        transform.SetParent(transform.root);
        transform.SetAsLastSibling();
        rootCanvasGroup.blocksRaycasts = false;

        _isDragging = true;
        _dragTargetPosition = _rectTransform.position;
        _followVelocity = Vector3.zero;
    }

    public void OnDrag(PointerEventData eventData)
    {
        RectTransform canvasRect = _canvas.transform as RectTransform;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, eventData.position, eventData.pressEventCamera, out Vector3 worldPosition))
        {
            _dragTargetPosition = worldPosition;
        }

        UpdateVisualContextUnderCursor(eventData);
    }

    private void UpdateVisualContextUnderCursor(PointerEventData eventData)
    {
        _raycastBuffer.Clear();
        EventSystem.current.RaycastAll(eventData, _raycastBuffer);

        bool? foundBelt = null;
        foreach (var hit in _raycastBuffer)
        {
            if (hit.gameObject == gameObject) continue;
            if ((uiRaycastMask.value & (1 << hit.gameObject.layer)) == 0) continue;

            var book = hit.gameObject.GetComponentInParent<BookController>();
            if (book != null)
            {
                foundBelt = !book._isBookOpen;
                break;
            }

            foundBelt = true;
            break;
        }

        if (foundBelt.HasValue && foundBelt != _lastBeltState)
        {
            SetVisualContext(foundBelt.Value);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _isDragging = false;
        StartDragAnimation();
        rootCanvasGroup.blocksRaycasts = true;
    }

    public void StartDragAnimation()
    {
        dragAnim?.Kill();

        dragAnim = transform.DOMove(parentAfterDrag.transform.position, 0.2f).OnComplete(
            () =>
            {
                transform.SetParent(parentAfterDrag.transform);
                
                if (sourceSlot != null && ReferenceEquals(parentAfterDrag, sourceSlot.transform))
                {
                    SetVisualContext(sourceSlot is HotSlots);
                }
            });
    }

    private void OnDestroy()
    {
        itemData.Item.OnQuantityChange -= UpdateQuantity;
        _healthComponent.OnCurrHealthDataChanged -= UpdateSlider;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClick?.Invoke();
    }
}

public class InventoryItemData
{
    public ItemStack Item;
    public int PageIndex;
    public int SlotIndex;

    public InventoryItemData(ItemStack stack, int page, int slot)
    {
        Item = stack;
        PageIndex = page;
        SlotIndex = slot;
    }
}