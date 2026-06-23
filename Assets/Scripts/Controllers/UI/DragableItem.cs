using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using DG.Tweening;
using Systems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

public class DragableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    private InventoryItemData _itemData;
    private HealthComponent _healthComponent;

    public InventoryItemData itemData
    {
        get
        {
            return _itemData;
        }
        set
        {
            _itemData = value;
            UpdateQuantity(1);
        }
    }
    public float draggingSpeed;

    private Transform _parentAfterDrag;
    [SerializeField] private Slider slider;
    [SerializeField] private Image sliderfill;
    [SerializeField] private TextMeshProUGUI tmPro;
    public int currPage;
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
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private CanvasGroup stickerCanvasGroup; 
    public int slotIndex;

    public Action OnClick;
    
    private RectTransform _rectTransform;
    public Image image;
    private Canvas _canvas;

    public Tween dragAnim;

    public SlotBase sourceSlot; 
    
    
    [SerializeField] private float followSmoothTime = 0.08f;
    private Vector3 _dragTargetPosition;
    private Vector3 _followVelocity;
    private bool _isDragging;
    
    
    [SerializeField] private Vector3 beltScale = Vector3.one * 1.3f;
    [SerializeField] private Vector3 bookScale = Vector3.one;
    [SerializeField] private float contextTweenDuration = 0.15f;
    
    
    [SerializeField] private LayerMask uiRaycastMask;
    private readonly List<RaycastResult> _raycastBuffer = new();
    private bool? _lastBeltState;

    private Tween _scaleTween;
    private Tween _stickerFadeTween;

    private void Start()
    {
        name = itemData.Item.itemName;
        itemData.Item.OnQuantityChange += UpdateQuantity;
        
        _rectTransform = transform as RectTransform;
        _canvas = GetComponentInParent<Canvas>();
    }
    
    public void SetVisualContext(bool isBelt)
    {
        _scaleTween?.Kill();
        _scaleTween = transform.DOScale(isBelt ? beltScale : bookScale, contextTweenDuration);

        _stickerFadeTween?.Kill();
        _stickerFadeTween = stickerCanvasGroup.DOFade(isBelt ? 0f : 1f, contextTweenDuration);
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

        if(quantity > 1)
            tmPro.text = $"{quantity}";
        else
        {
            tmPro.text = String.Empty;
        }
    }

    public void UpdateSlider(float health)
    {
        if (_healthComponent == null)
            _healthComponent = itemData.Item.GetItemComponent<HealthComponent>();
        
        slider.maxValue = _healthComponent.maxHealth;
        slider.value = health;
        var percent = slider.value / slider.maxValue;
        
        if (percent < 0.8f)
        {
            sliderfill.color = new Color32(255, (byte)(255 * percent), 0, (byte)(120 * (1.3f - percent)));
        }
        else
        {
            sliderfill.color = new Color32(255, (byte)(255 * percent), 0, 0);
        }
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

            // нашли валидный UI-хит, но это не книга (например, ремень) — считаем ремнём
            foundBelt = true;
            break;
        }

        if (foundBelt.HasValue && foundBelt != _lastBeltState)
        {
            _lastBeltState = foundBelt;
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

        dragAnim = transform.DOMove(parentAfterDrag.transform.position,0.2f).OnComplete(
            () =>
            {
                transform.SetParent(parentAfterDrag.transform);
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

    public InventoryItemData(ItemStack stack,int page, int slot)
    {
        Item = stack;
        PageIndex = page;
        SlotIndex = slot;
    }
}