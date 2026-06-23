using System.Collections.Generic;
using System.Linq;
using Controllers;
using Systems;
using UnityEngine;
using UnityEngine.EventSystems;

public class StorageGrid : MonoBehaviour, IDropHandler
{
    private InventorySlotsComponent _inventorySlotsComponent;
    private InventoryComponent _inventoryComponent;
    private InventoryViewComponent _inventoryViewComponent;

    private StorageSlot[] _slots;
    private readonly Dictionary<ItemStack, DragableItem> _visuals = new();

    public void InitializeGrid(AbstractEntity owner, InventorySlotsComponent slotsComponent,
        InventoryComponent inventoryComponent, InventoryViewComponent viewComponent)
    {
        _inventorySlotsComponent = slotsComponent;
        _inventoryComponent = inventoryComponent;
        _inventoryViewComponent = viewComponent;

        _slots = GetComponentsInChildren<StorageSlot>();
        for (int i = 0; i < _slots.Length; i++)
            _slots[i].Init((i, owner));

        _inventoryViewComponent.storageCount = _slots.Length;
        _inventorySlotsComponent.storageSlots = _slots;

        _inventoryComponent.storage.OnItemChanged += OnStorageChanged;

        Rebuild();
    }

    public void DisposeGrid()
    {
        _inventoryComponent.storage.OnItemChanged -= OnStorageChanged;
    }

    // Дроп в пустую область самого грида (не на конкретный предмет) —
    // забираем стек из его текущего списка и добавляем в конец storage.
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

        if (!HasFreeCell(out var emptySlot))
            return;

        if (dragItem.sourceSlot == null)
            return;

        // регистрируем визуал ДО того, как сработает событие списка —
        // Reconcile, вызванный синхронно внутри SwapItems/SwapOrMoveItems,
        // должен найти его здесь, а не спавнить новый
        _visuals[stack] = dragItem;

        emptySlot.SwapItems(dragItem);
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
    }

    public void Rebuild()
    {
        foreach (var slot in _slots)
            slot.DestroyVisual();
        _visuals.Clear();

        Reconcile();
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
            _inventorySlotsComponent.slots[globalIndex] = slot;

            if (current != null && ReferenceEquals(current.itemData.Item, data.Item))
                continue;

            if (_visuals.TryGetValue(data.Item, out var existing) && existing != null)
            {
                // тот же визуал может прямо сейчас "висеть" в другом слоте этого
                // же грида — снимаем с него ссылку, не уничтожая объект, иначе
                // его собственная итерация ниже/выше по циклу решит, что он
                // "пропал" и убьёт его
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