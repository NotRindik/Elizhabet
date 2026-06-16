using System;
using System.Collections;
using System.Diagnostics;
using DG.Tweening;
using Systems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

public class DragableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler,IDropHandler
{
    private InventoryItemData _itemData;
    private InventorySystem _inventorySystem;
    private HealthComponent _healthComponent;
    public InventorySystem.SlotRef SourceSlot { get; private set; }
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
            _parentAfterDrag = value; 
            //StartDragAnimation();
        }
    }
    public Image image;
    public CanvasGroup cv;
    public int slotIndex;
    public Coroutine DragAnimationProcess;

    public Action OnClick;
    
    private RectTransform _rectTransform;
    private Canvas _canvas;

    private Tween _dragTween;
    private Vector3 _dragOffset;
    
    
    public void SetParent(Transform parent)
    {
        parentAfterDrag = parent;
    }
    
    private void Start()
    {
        name = itemData.Item.itemName;
        
        itemData.Item.OnQuantityChange += UpdateQuantity;
        
        _rectTransform = transform as RectTransform;
        _canvas = GetComponentInParent<Canvas>();
    }
    
    public void SetSourceSlot(InventorySystem.SlotRef slotRef)
    {
        SourceSlot = slotRef;
    }
    
    public void Init(ItemStack stack, InventorySystem.SlotRef sourceSlot, InventorySystem system)
    {
        itemData   = new InventoryItemData(stack, 0, sourceSlot.Index);
        SourceSlot = sourceSlot;
        _inventorySystem = system;
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
        _dragTween?.Kill();
        
        parentAfterDrag = transform.parent;
        
        RectTransform canvasRect = _canvas.transform as RectTransform;
        
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            canvasRect, eventData.position, eventData.pressEventCamera, out Vector3 worldMousePos);
        
        _dragOffset = _rectTransform.position - worldMousePos;
    
        transform.SetParent(transform.root);
        transform.SetAsLastSibling();
        cv.blocksRaycasts = false;
    }
    

    public void OnDrag(PointerEventData eventData)
    {
        RectTransform canvasRect = _canvas.transform as RectTransform;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, eventData.position, eventData.pressEventCamera, out Vector3 worldPosition))
        {
            _rectTransform.position = worldPosition + _dragOffset;
        }
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        StartDragAnimation();
        cv.blocksRaycasts = true;
    }

    public void StartDragAnimation()
    {
        _dragTween?.Kill();

        _dragTween = transform.DOMove(parentAfterDrag.position, 0.4f).OnComplete(() => transform.SetParent(parentAfterDrag.transform));
    }

    private void OnDestroy()
    {
        itemData.Item.OnQuantityChange -= UpdateQuantity;
        if (_healthComponent != null)
            _healthComponent.OnCurrHealthDataChanged -= UpdateSlider;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClick?.Invoke();
    }
    
    public void OnDrop(PointerEventData eventData)
    {
        var dragItem = eventData.pointerDrag?.GetComponent<DragableItem>();
        if (dragItem == null) return;
        
        _inventorySystem.MoveOrSwap(dragItem.SourceSlot, SourceSlot);
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