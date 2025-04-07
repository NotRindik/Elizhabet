using System;
using System.Collections.Generic;
using System.Linq;
using Controllers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Systems
{
    public class InventorySystem : BaseSystem
    {
        InventoryComponent _inventoryComponent;
        ColorPositioningComponent colorPositioning;
        private EntityController _owner;

        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _owner = (EntityController)owner;
            _inventoryComponent = _owner.GetControllerComponent<InventoryComponent>();
            colorPositioning = _owner.GetControllerComponent<ColorPositioningComponent>();
        }

        public void TakeItem(InputAction.CallbackContext callback) 
        {
            Collider2D nearestItem = Physics2D.OverlapCircleAll(_owner.transform.position, _inventoryComponent.itemCheckRadius, _inventoryComponent.itemLayer)
                .OrderBy(item => Vector2.Distance(item.transform.position, _owner.transform.position))
                .FirstOrDefault();
            
            if (nearestItem != null)
            {
                var item = nearestItem.GetComponent<Items>();

                for (int i = 0; i < _inventoryComponent.items.Count; i++)
                {
                    var existItem = _inventoryComponent.items[i];

                    if (existItem.itemName == item.itemComponent.itemPrefab.name)
                    {
                        existItem.AddItem(item.itemComponent);
                        MonoBehaviour.Destroy(item.gameObject);
                        return;
                    }
                }
                var stack = new ItemStack(item.itemComponent.itemPrefab.name);
                stack.AddItem(item.itemComponent);
                _inventoryComponent.items.Add(stack);
                
                if (_inventoryComponent.ActiveItem == null)
                {
                    item.TakeUp(colorPositioning, _owner);
                    _inventoryComponent.ActiveItem = item;
                    _inventoryComponent.ActiveItem.itemComponent.currentOwner = _owner;
                }
                else
                {
                    MonoBehaviour.Destroy(item.gameObject);
                }
            }
        }

        public void ThrowItem(InputAction.CallbackContext callback)
        {
            if (_inventoryComponent.ActiveItem)
            {
                _inventoryComponent.ActiveItem.Throw();
                var stack = _inventoryComponent.items[_inventoryComponent.CurrentActiveIndex];
                stack.RemoveItem(_inventoryComponent.ActiveItem.itemComponent);
                
                _inventoryComponent.ActiveItem = null;
                if (stack.items.Count == 0)
                    _inventoryComponent.items.Remove(stack);
                if (_inventoryComponent.items.Contains(stack))
                {
                    SetActiveWeaponWithoutDestroy(_inventoryComponent.items.FindIndex(element => element.itemName == stack.itemName));
                }
            }
        }

        public void PreviousItem(InputAction.CallbackContext callbackContext)
        {
            if (_inventoryComponent.CurrentActiveIndex >= 0)
            {
                SetActiveWeapon(_inventoryComponent.CurrentActiveIndex-1);
            }
        }

        public void NextItem(InputAction.CallbackContext callbackContext)
        {
            if (_inventoryComponent.CurrentActiveIndex < _inventoryComponent.items.Count - 1)
            {
                SetActiveWeapon(_inventoryComponent.CurrentActiveIndex + 1);
            }
        }

        public void SetActiveWeapon(int index)
        {
            GameObject.Destroy(_inventoryComponent.ActiveItem?.gameObject);
            SetActiveWeaponWithoutDestroy(index);
        }
        public void SetActiveWeaponWithoutDestroy(int index)
        {
            if (index > -1)
            {
                GameObject inst = (GameObject)GameObject.Instantiate(_inventoryComponent.items[index].items[0].itemPrefab);
                var item = inst.GetComponent<Items>();
                item.InitAfterSpawnFromInventory(_inventoryComponent.items[index].items[0]);
                _inventoryComponent.ActiveItem = item;
                _inventoryComponent.ActiveItem.TakeUp(colorPositioning, _owner);
                _inventoryComponent.ActiveItem.itemComponent.currentOwner = _owner;
            }
            else
            {
                _inventoryComponent.ActiveItem = null;
            }
        }
    }

    [System.Serializable]
    public class InventoryComponent: IComponent
    {
        public float itemCheckRadius = 2f;
        public LayerMask itemLayer;
        public int CurrentActiveIndex => ActiveItem != null ?items.FindIndex(stack => stack.itemName == ActiveItem.itemComponent.itemPrefab.name) : -1;

        public List<ItemStack> items = new List<ItemStack>();
        
        public event Action<Items,Items> OnActiveItemChange;

        public void ClearEventRef()
        {
            OnActiveItemChange = null;
        }

        private Items _activeItem;

        public Items ActiveItem
        {
            get
            {
                return _activeItem;
            }

            set
            {
                var tempPrevItem = _activeItem;
                _activeItem = value;
                OnActiveItemChange?.Invoke(_activeItem,tempPrevItem);
            }
        }
    }
    
    [System.Serializable]
    public class ItemStack
    {
        public string itemName;
        public List<ItemComponent> items = new List<ItemComponent>();
        public event Action<int> OnQuantityChange;
        public ItemStack(string name)
        {
            itemName = name;
        }

        public void AddItem(ItemComponent item)
        {
            items.Add(item);
            OnQuantityChange?.Invoke(items.Count);
        }

        public void RemoveItem(ItemComponent item)
        {
            items.Remove(item);
            OnQuantityChange?.Invoke(items.Count);
        }

        public int Count => items.Count;
    }
}