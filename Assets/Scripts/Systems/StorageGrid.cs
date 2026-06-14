using Systems;
using UnityEngine;
using UnityEngine.EventSystems;

public class StorageGrid : MonoBehaviour, IDropHandler
{
    private InventorySystem _inventorySystem;
    private InventoryComponent _inv;

    public void Init(AbstractEntity owner)
    {
        _inventorySystem = owner.GetControllerSystem<InventorySystem>();
        _inv = owner.GetControllerComponent<InventoryComponent>();
    }

    // Дроп на пустое место грида — перемещаем в конец storage
    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log("Dropped");
        var dragItem = eventData.pointerDrag?.GetComponent<DragableItem>();
        if (dragItem == null) return;

        int freeIndex = _inv.storage.items.FindIndex(s => s == null);
        if (freeIndex == -1) freeIndex = _inv.storage.items.Count;

        _inventorySystem.MoveOrSwap(
            dragItem.SourceSlot,
            new InventorySystem.SlotRef(isHotBar: false, freeIndex)
        );
    }
}