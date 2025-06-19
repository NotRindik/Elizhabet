using System;
using System.Collections.Generic;
using System.Linq;
using AYellowpaper.SerializedCollections;
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
                var item = nearestItem.GetComponent<Item>();

                for (int i = 0; i < _inventoryComponent.items.Count; i++)
                {
                    var existItem = _inventoryComponent.items[i];

                    if (existItem.itemName == item.itemComponent.itemPrefab.name)
                    {
                        existItem.AddItem(item.itemComponent);
                        existItem.items = existItem.items
                            .OrderBy(component =>
                            {
                                DurabilityComponent durabilityComponent = (DurabilityComponent)component[typeof(DurabilityComponent)];
                                return durabilityComponent != null ? durabilityComponent.Durability : int.MaxValue;
                            })
                            .ToList();
                        SetActiveWeapon(_inventoryComponent.CurrentActiveIndex);
                        Object.Destroy(item.gameObject);
                        return;
                    }
                }
                var stack = new ItemStack(item.itemComponent.itemPrefab.name,_inventoryComponent);
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
                    Object.Destroy(item.gameObject);
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
                GameObject inst = Object.Instantiate(((ItemComponent)_inventoryComponent.items[index].items[0][typeof(ItemComponent)]).itemPrefab);
                var item = inst.GetComponent<Item>();
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
        
        public event Action<Item,Item> OnActiveItemChange;

        private Item _activeItem;

        public Item ActiveItem
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
        [System.NonSerialized] public InventoryComponent inventoryComponent;
        public List<SerializedDictionary<Type, IComponent>> items = new List<SerializedDictionary<Type, IComponent>>();
        public event Action<int> OnQuantityChange;
        public ItemStack(string name, InventoryComponent inventoryComponent)
        {
            itemName = name;
            this.inventoryComponent = inventoryComponent;
        }

        public void AddItem(ItemComponent item)
        {
            items.Add(item);
            AutoDestruct(); 
            OnQuantityChange?.Invoke(items.Count);
        }

        public void RemoveItem(ItemComponent item)
        {
            items.Remove(item);
            AutoDestruct();
            OnQuantityChange?.Invoke(items.Count);
        }

        public void AutoDestruct()
        {
            if(Count == 0)
            {
                OnQuantityChange = null;
                inventoryComponent.items.Remove(this);
            }
        }

        public int Count => items.Count;
    }
}