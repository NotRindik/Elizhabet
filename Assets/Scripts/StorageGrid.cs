using System.Collections.Generic;
using System.Linq;
using Controllers;
using DG.Tweening;
using Systems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class StorageGrid : MonoBehaviour, IDropHandler
{
    private InventorySlotsComponent _inventorySlotsComponent;
    private InventoryComponent _inventoryComponent;
    private InventoryViewComponent _inventoryViewComponent;

    private StorageSlot[] _slots;
    private readonly Dictionary<ItemStack, DragableItem> _visuals = new();
    
    private Tween _fullTextTween;
    private string _cachedText;

    public void InitializeGrid(AbstractEntity owner, InventorySlotsComponent slotsComponent, InventoryComponent inventoryComponent, InventoryViewComponent viewComponent)
    {
        _inventorySlotsComponent = slotsComponent;
        _inventoryComponent = inventoryComponent;
        _inventoryViewComponent = viewComponent;

        _slots = GetComponentsInChildren<StorageSlot>();
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i].Init((i, owner));
            _slots[i].OnDropFailed += TryShowFullText;
        }
        _inventoryViewComponent.storageCount = _slots.Length;
        _inventorySlotsComponent.storageSlots = _slots;

        _inventoryComponent.storage.observableList.OnItemChanged += OnStorageChanged;
        
        Rebuild();
    }

    public void RecountLimit()
    {
        var filter = _inventoryViewComponent.Filter;
        ItemCategory filterCategory = ItemCategory.None;
        if(filter != null)
            filterCategory = InventoryFilters.FilterTypes[filter.GetType()];
        var (current, limit) = _inventoryComponent.storage.GetCategoryFill(filterCategory.ToString());

        if (filterCategory != ItemCategory.None)
        {
            _inventorySlotsComponent.storageCapacityText.text = $"Capacity: {current}/{limit}";
        }
        else
        {
            _inventorySlotsComponent.storageCapacityText.text = $"Capacity: {current}";
        }
    }

    public void DisposeGrid()
    {
        _inventoryComponent.storage.observableList.OnItemChanged -= OnStorageChanged;
        
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i].OnDropFailed -= TryShowFullText;
        }
    }
    
    public void OnDrop(PointerEventData eventData)
    {
        
        var dropped = eventData.pointerDrag;
        if (dropped == null) return;

        var dragItem = dropped.GetComponent<DragableItem>();
        if (dragItem == null) return;

        var stack = dragItem.itemData.Item;
        if (stack == null) return;

        if (_inventoryComponent.storage.Raw.Contains(stack))
            return;

        TryShowFullText(dragItem);
        
        if (!HasFreeCell(out var emptySlot))
            return;

        if (dragItem.sourceSlot == null)
            return;
        
        _visuals[stack] = dragItem;

        emptySlot.SwapItems(dragItem);
    }
    private void TryShowFullText(DragableItem dragable)
    {
        if (!_inventoryComponent.storage.CanAdd(dragable.itemData.Item))
        {
            ShowFullText();
        }
    }
    private void ShowFullText()
    {
        TMP_Text text = _inventorySlotsComponent.storageCapacityText;

        if (_fullTextTween == null || !_fullTextTween.IsActive())
            _cachedText = text.text;

        _fullTextTween?.Kill();

        text.transform.localScale = Vector3.one;
        text.text = "<color=red>FULL";

        _fullTextTween = DOTween.Sequence()
            .Append(text.transform.DOPunchScale(Vector3.one * 0.2f, 0.25f))
            .AppendInterval(0.6f)
            .AppendCallback(() =>
            {
                text.text = _cachedText;
                text.transform.localScale = Vector3.one;
            });
    }
    

    private bool HasFreeCell(out StorageSlot slot)
    {
        StorageSlot sl = null;
        var result = _slots.Any(s =>
            {
                sl = s;
                return s.IsEmpty;
            }
        );
        
        slot = sl;
        return result;
    }

    private void OnStorageChanged(ItemStack _stack)
    {
        Reconcile();

        RecountLimit();
    }

    public void Rebuild()
    {
        foreach (var slot in _slots)
            slot.DestroyVisual();
        _visuals.Clear();

        Reconcile();
        
        RecountLimit();
    }

    private void Reconcile()
    {
        var visible = BuildVisibleWindow();
        int storageOffset = _inventoryComponent.hotBar.Count
                            + _inventoryComponent.armor.Count
                            + _inventoryComponent.accessories.Count;

        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            var current = slot.GetItem();

            _inventorySlotsComponent.slots.Remove(slot.Index);

            if (i >= visible.Count)
            {
                if (current != null)
                {
                    _visuals.Remove(current.itemData.Item);
                    slot.DestroyVisual();
                }
                continue;
            }

            var (data, flatIndex) = visible[i];
            int globalIndex = storageOffset + flatIndex;

            slot.currPage = _inventoryViewComponent.page;
            slot.BoundStorageIndex = flatIndex; 
            _inventorySlotsComponent.slots[globalIndex] = slot;

            if (current != null && ReferenceEquals(current.itemData.Item, data.Item))
                continue;

            if (_visuals.TryGetValue(data.Item, out var existing) && existing != null)
            {
                var previousOwner = _slots.FirstOrDefault(s => s != slot && s.GetItem() == existing);
                previousOwner?.Clear();

                slot.AttachExisting(existing);
                continue;
            }

            slot.SetData(data);

            var spawned = slot.GetItem();
            if (spawned != null)
                _visuals[data.Item] = spawned;
        }
    }

    private List<(InventoryItemData data, int flatStorageIndex)> BuildVisibleWindow()
    {
        int page = _inventoryViewComponent.page;
        int take = _slots.Length;
        var raw = _inventoryComponent.storage.Raw;

        var matches = new List<(InventoryItemData, int)>();
        for (int idx = 0; idx < raw.Count; idx++)
        {
            var stack = raw[idx];
            if (stack == null) continue;

            var data = new InventoryItemData(stack, page, -1);
            if (_inventoryViewComponent.FilterAllows(data))
                matches.Add((data, idx));
        }

        return matches.Skip(page * take).Take(take).ToList();
    }
}