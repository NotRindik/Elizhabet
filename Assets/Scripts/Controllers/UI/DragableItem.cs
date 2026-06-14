using System;
using System.Collections;
using Systems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
            StartDragAnimation();
        }
    }
    public Image image;
    public int slotIndex;
    public Coroutine DragAnimationProcess;

    public Action OnClick;
    
    private RectTransform _rectTransform;
    private Canvas _canvas;
    
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
    private Vector3 _dragOffset;

    public void OnBeginDrag(PointerEventData eventData)
    {
        parentAfterDrag = transform.parent;
    
        // Запоминаем смещение между позицией объекта и позицией мыши
        RectTransform canvasRect = _canvas.transform as RectTransform;
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            canvasRect, eventData.position, eventData.pressEventCamera, out Vector3 worldMousePos);
        _dragOffset = _rectTransform.position - worldMousePos;
    
        transform.SetParent(transform.root);
        transform.SetAsLastSibling();
        image.raycastTarget = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        RectTransform canvasRect = _canvas.transform as RectTransform;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvasRect, eventData.position, eventData.pressEventCamera, out Vector3 worldPosition))
        {
            _rectTransform.position = worldPosition + _dragOffset;
        }
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        //        StartDragAnimation();
        image.raycastTarget = true;
    }

    public void StartDragAnimation()
    {
        if(DragAnimationProcess != null)
            StopCoroutine(DragAnimationProcess);
        DragAnimationProcess = StartCoroutine(DragAnimation());
    }

    public IEnumerator DragAnimation()
    {
        while (Vector2.Distance(parentAfterDrag.position, transform.position) > 0.2f)
        {
            float distance = Vector2.Distance(parentAfterDrag.position, transform.position);
            // Скорость увеличивается с расстоянием, но с учетом времени
            float speed = draggingSpeed * Mathf.Max(1f, Mathf.Min(distance * 0.2f,4));
        
            yield return new WaitForFixedUpdate();
            transform.position = Vector2.MoveTowards(transform.position, parentAfterDrag.position, speed);
        }
        DragAnimationProcess = null;
        transform.SetParent(parentAfterDrag);
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