using System;
using System.Collections;
using Systems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    private ItemStack _itemData;
    private HealthComponent _healthComponent;

    public ItemStack itemData
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

    private void Start()
    {
        name = itemData.itemName;
        itemData.OnQuantityChange += UpdateQuantity;
    }
    public void UpdateQuantity(int quantity)
    {
        if (_healthComponent == null)
        {
            _healthComponent = itemData.GetItemComponent<HealthComponent>();
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
            _healthComponent = itemData.GetItemComponent<HealthComponent>();
        
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
        image.raycastTarget = false;
    }
    public void OnDrag(PointerEventData eventData)
    {
        transform.position = UnityEngine.Input.mousePosition;
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        StartDragAnimation();
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
        itemData.OnQuantityChange -= UpdateQuantity;
        _healthComponent.OnCurrHealthDataChanged -= UpdateSlider;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClick?.Invoke();
    }
}
