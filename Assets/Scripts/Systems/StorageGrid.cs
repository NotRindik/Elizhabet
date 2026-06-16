using System.Collections.Generic;
using NUnit.Framework;
using Systems;
using UnityEngine;
using UnityEngine.EventSystems;

public class StorageGrid : MonoBehaviour, IDropHandler
{
    private InventorySystem _inventorySystem;
    private InventoryComponent _inv;

    public GameObject[] gridSlots;

    public List<DragableItem> dragableItems = new List<DragableItem>();
    
    public void Init(AbstractEntity owner)
    {
        _inventorySystem = owner.GetControllerSystem<InventorySystem>();
        _inv = owner.GetControllerComponent<InventoryComponent>();
        
        gridSlots = new GameObject[transform.childCount];

        for (int i = 0; i < gridSlots.Length; i++)
        {
            gridSlots[i] = transform.GetChild(i).gameObject;
        }
    }
    
    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log("Dropped");
        var dragItem = eventData.pointerDrag?.GetComponent<DragableItem>();
        if (dragItem == null) return;

        int freeIndex = _inv.storage.items.FindIndex(s => s == null);
        if (freeIndex == -1) freeIndex = _inv.storage.items.Count;
        
        dragableItems.Add(dragItem);
        
         _inventorySystem.MoveOrSwap(
             dragItem.SourceSlot,
             new InventorySystem.SlotRef(isHotBar: false, freeIndex)
        );
    }
}