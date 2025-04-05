using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts;
using Controllers;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Systems
{
    public class InventorySystem : BaseSystem
    {
        InventoryComponent _inventoryComponent;
        ColorPositioningComponent colorPositioning;
        private EntityController owner;

        public void Initialize(Controller owner, InventoryComponent inventoryComponent,ColorPositioningComponent colorPositioning)
        {
            base.Initialize(owner);
            this.owner = (EntityController)owner;
            this._inventoryComponent = inventoryComponent;
            this.colorPositioning = colorPositioning;
        }

        public void TakeItem(InputAction.CallbackContext callback) 
        {
            Collider2D nearestItem = Physics2D.OverlapCircleAll(owner.transform.position, _inventoryComponent.itemCheckRadius, _inventoryComponent.itemLayer)
                .OrderBy(item => Vector2.Distance(item.transform.position, owner.transform.position))
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
                    item.TakeUp(colorPositioning, owner);
                    _inventoryComponent.ActiveItem = item;
                    _inventoryComponent.ActiveItem.itemComponent.currentOwner = owner;
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
                
                var stack = _inventoryComponent.items.FirstOrDefault(stack => stack.itemName == _inventoryComponent.ActiveItem.itemComponent.itemPrefab.name);
                
                if(stack != null && stack.items.Count == 0)
                    _inventoryComponent.items.Remove(stack);
                
                _inventoryComponent.ActiveItem = null;
                if (_inventoryComponent.items.Count > 0)
                {
                    SetActiveWeaponWithoutDestroy(_inventoryComponent.items.FindIndex(element => stack.itemName == stack.itemName));
                }
            }
        }

        public void PreviousItem(InputAction.CallbackContext callbackContext)
        {
            if (_inventoryComponent.currentActiveIndex > 0)
            {
                _inventoryComponent.currentActiveIndex--;
                SetActiveWeapon(_inventoryComponent.currentActiveIndex);
            }
        }

        public void NextItem(InputAction.CallbackContext callbackContext)
        {
            if (_inventoryComponent.currentActiveIndex < _inventoryComponent.items.Count - 1)
            {
                _inventoryComponent.currentActiveIndex++;
                SetActiveWeapon(_inventoryComponent.currentActiveIndex);
            }
        }

        public void SetActiveWeapon(int index)
        {
            GameObject.Destroy(_inventoryComponent.ActiveItem.gameObject);
            SetActiveWeaponWithoutDestroy(index);
        }
        public void SetActiveWeaponWithoutDestroy(int index)
        {
            GameObject inst = (GameObject)GameObject.Instantiate(_inventoryComponent.items[index].items[0].itemPrefab);
            var item = inst.GetComponent<Items>();
            item.InitAfterSpawnFromInventory(_inventoryComponent.items[index].items[0]);
            _inventoryComponent.ActiveItem = item;
            _inventoryComponent.ActiveItem.TakeUp(colorPositioning, owner);
            _inventoryComponent.ActiveItem.itemComponent.currentOwner = owner;
        }
    }

    [System.Serializable]
    public class InventoryComponent: IComponent
    {
        public float itemCheckRadius = 2f;
        public LayerMask itemLayer;
        public int currentActiveIndex;

        public List<ItemStack> items = new List<ItemStack>();

        public Action<Items,Items> OnActiveItemChange;

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
        public Action<int> OnQuantityChange;
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