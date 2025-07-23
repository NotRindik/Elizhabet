using System;
using System.Collections.Generic;
using System.Linq;
using Controllers;
using UnityEngine;
using AYellowpaper.SerializedCollections;
using UnityEngine.Serialization;

namespace Systems
{
    public class InventorySlotsSystem : BaseSystem,IDisposable
    {
        private InventorySlotsComponent _inventorySlotsComponent;
        private InventoryComponent _inventoryComponent;
        private InventorySystem _inventorySystem;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _inventorySlotsComponent = owner.GetControllerComponent<InventorySlotsComponent>();
            _inventorySystem = owner.GetControllerSystem<InventorySystem>();
            _inventoryComponent = base.owner.GetControllerComponent<InventoryComponent>();
            
            _inventorySlotsComponent.slots = _inventorySlotsComponent.slotsContainers
                .SelectMany(container => container.Value.GetComponentsInChildren<SlotBase>())
                .ToArray();
            
            for (int i = 0; i < _inventorySlotsComponent.slots.Length; i++)
            {
                _inventorySlotsComponent.slots[i].Init((i, owner));
            }
            _inventorySlotsComponent.conveyorSlots = _inventorySlotsComponent.slots
                .Where((slot, index) => index <= 4 && slot is СonveyorSlot)
                .Cast<СonveyorSlot>()
                .ToArray();
            
            _inventoryComponent.items.OnItemAdded += AddItem;
            _inventoryComponent.items.OnItemRemoved += CleanItem;
        }

        private void AddItem(ItemStack itemStack)
        {
            if(!IsActive)
                return;
            Debug.Log("Add");
            for (int i = 0; i < _inventorySlotsComponent.slots.Length; i++)
            {
                if (_inventorySlotsComponent.slots[i].IsEmpty)
                {
                    _inventorySlotsComponent.slots[i].SetData(itemStack);
                    break;
                }
            }
            _inventoryComponent.hotSlotCongestion = 0;
            for (int i = 0; i < 5; i++)
            {
                if (_inventorySlotsComponent.slots[i].transform.childCount != 0)
                {
                    _inventoryComponent.hotSlotCongestion++;
                }
            }
        }

        private void CleanItem(ItemStack itemStack)
        {
            if(!IsActive)
                return;
            
            for (int i = 0; i < _inventorySlotsComponent.slots.Length; i++)
            {
                var item = _inventorySlotsComponent.slots[i].GetItem();
                if (item != null && item.itemData == itemStack)
                {
                    _inventorySlotsComponent.slots[i].Clear();
                }
            }
            
            _inventoryComponent.hotSlotCongestion = 0;
            for (int i = 0; i < 5; i++)
            {
                if (_inventorySlotsComponent.slots[i].transform.childCount != 0)
                {
                    _inventoryComponent.hotSlotCongestion++;
                }
            }
        }
        
        public void Dispose()
        {
            _inventoryComponent.items.OnItemAdded -= AddItem;
            _inventoryComponent.items.OnItemRemoved -= CleanItem;
        }
    }
    [System.Serializable]
    public class InventorySlotsComponent : IComponent
    {
        public SerializedDictionary<string,GameObject> slotsContainers;

        public DragableItem itemPrefab;

        public SlotBase[] slots;
        public СonveyorSlot[] conveyorSlots; 
    }
}