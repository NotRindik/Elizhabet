using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Assets.Scripts;
using Controllers;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Systems
{
    public class InventorySystem : BaseSystem,IDisposable
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
            _inventoryComponent.OnActiveItemChange += OnActiveItemChange;
        }
        private void OnActiveItemChange(Item past,Item curr)
        {
            if (past)
            {
                past.OnRequestDestroy -= OnItemDestroy;
            }
            if (curr)
            {
                curr.OnRequestDestroy += OnItemDestroy;
            }
        }

        public void TakeItem() 
        {
            Collider2D nearestItem = Physics2D.OverlapCircleAll(_owner.transform.position, _inventoryComponent.itemCheckRadius, _inventoryComponent.itemLayer)
                .OrderBy(item => Vector2.Distance(item.transform.position, _owner.transform.position))
                .FirstOrDefault();
            
            if (nearestItem != null)
            {
                var item = nearestItem.GetComponent<Item>();

                for (int i = 0; i < _inventoryComponent.items.Count; i++)
                {
                    var existStack = _inventoryComponent.items[i];

                    if (existStack.itemName == item.itemComponent.itemPrefab.name)
                    {
                        existStack.AddItem(item.Components);
                        existStack.items = existStack.items
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
                stack.AddItem(item.Components);
                _inventoryComponent.items.Add(stack);
                
                if (_inventoryComponent.ActiveItem == null)
                {
                    item.SelectItem(_owner);
                    _inventoryComponent.ActiveItem = item;
                    _inventoryComponent.ActiveItem.itemComponent.currentOwner = _owner;
                }
                else
                {
                    Object.Destroy(item.gameObject);
                }
            }
        }
        public void OnItemDestroy(Item item)
        {
            if (item.durabilityComponent.Durability > 0)
            {
                return;
            }
            int index = _inventoryComponent.items.FindIndex(itemStack => itemStack.itemName == item.itemComponent.itemPrefab.name);
            var stack = _inventoryComponent.items[index];
        
            stack.RemoveItem(item.Components);
        
            if (!_inventoryComponent.items.Contains(stack))
            {
                int clampedIndex = Mathf.Clamp(
                    _inventoryComponent.CurrentActiveIndex, 
                    0, 
                    Mathf.Max(_inventoryComponent.items.Count - 1, 0)
                );
            
                int newActiveIndex;
                if (clampedIndex < _inventoryComponent.items.Count - 1)
                {
                    newActiveIndex = clampedIndex + 1;
                }
                else if (clampedIndex > 0)
                {
                    newActiveIndex = clampedIndex - 1;
                }
                else
                {
                    newActiveIndex = 0;
                }
            
                if (newActiveIndex >= 0 && newActiveIndex < _inventoryComponent.items.Count)
                {
                    SetActiveWeaponWithoutDestroy(newActiveIndex);
                }
                else
                {
                    SetActiveWeaponWithoutDestroy(-1);
                }
            }
            else
            {
                SetActiveWeaponWithoutDestroy(index);
            }

        }

        public void ThrowItem()
        {
            if (_inventoryComponent.ActiveItem)
            {
                _inventoryComponent.ActiveItem.Throw();
                var stack = _inventoryComponent.items[_inventoryComponent.CurrentActiveIndex];
                stack.RemoveItem(_inventoryComponent.ActiveItem.Components);
                _inventoryComponent.ActiveItem = null;
                if (_inventoryComponent.items.Contains(stack))
                {
                    SetActiveWeaponWithoutDestroy(_inventoryComponent.items.FindIndex(element => element.itemName == stack.itemName));
                }
            }
        }
        public void PreviousItem()
        {
            if (_inventoryComponent.CurrentActiveIndex >= 0)
            {
                SetActiveWeapon(_inventoryComponent.CurrentActiveIndex-1);
            }
        }

        public void NextItem()
        {
            if (_inventoryComponent.CurrentActiveIndex < _inventoryComponent.items.Count - 1)
            {
                SetActiveWeapon(_inventoryComponent.CurrentActiveIndex + 1);
            }
        }

        private void SetActiveWeapon(int index)
        {
            Object.Destroy(_inventoryComponent.ActiveItem?.gameObject);
            SetActiveWeaponWithoutDestroy(index);
        }
        private void SetActiveWeaponWithoutDestroy(int index)
        {
            if (index > -1)
            {
                Debug.Log(((ItemComponent)_inventoryComponent.items[index].items[0][typeof(ItemComponent)]).itemPrefab);
                GameObject inst = Object.Instantiate(((ItemComponent)_inventoryComponent.items[index].items[0][typeof(ItemComponent)]).itemPrefab);
                var item = inst.GetComponent<Item>();
                item.InitAfterSpawnFromInventory(_inventoryComponent.items[index].items[0]);
                _inventoryComponent.items[index].items[0] = item.Components;
                Debug.Log(((ItemComponent)_inventoryComponent.items[index].items[0][typeof(ItemComponent)]).itemPrefab);
                _inventoryComponent.ActiveItem = item;
                _inventoryComponent.ActiveItem.SelectItem(_owner);
                _inventoryComponent.ActiveItem.itemComponent.currentOwner = _owner;
            }
            else
            {
                _inventoryComponent.ActiveItem = null;
            }
        }
        public void Dispose()
        {
            _inventoryComponent.OnActiveItemChange -= OnActiveItemChange;
        }
    }

    [System.Serializable]
    public class InventoryComponent: IComponent
    {
        public float itemCheckRadius = 2f;
        public LayerMask itemLayer;
        public int CurrentActiveIndex => ActiveItem != null ?items.FindIndex(stack =>
            {
                return stack.itemName == _activeItem.itemComponent.itemPrefab.name;
            }
        ) : -1;

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
    public class ItemStack:IDisposable
    {
        public string itemName;
        [System.NonSerialized] public InventoryComponent inventoryComponent;
        public List<Dictionary<Type, IComponent>> items = new List<Dictionary<Type, IComponent>>();
        public List<string> components = new List<string>();
        public int count;
        public event Action<int> OnQuantityChange;
        public ItemStack(string name, InventoryComponent inventoryComponent)
        {
            itemName = name;
            this.inventoryComponent = inventoryComponent;
            OnQuantityChange += count =>
            {
                if (count == 0)
                    Dispose();
            };
            OnQuantityChange += c => UpdateComponentSerialization();
        }

        public void AddItem(Dictionary<Type, IComponent> item)
        {
            items.Add(item);
            OnQuantityChange?.Invoke(Count);
        }

        public void RemoveItem(Dictionary<Type, IComponent> item)
        {
            items.Remove(item);
            OnQuantityChange?.Invoke(Count);
        }
        private void UpdateComponentSerialization()
        {
            count = Count;
            if (Count == 0)
                return;
            components.Clear();
            foreach (var key in items[0].Keys)
            {
                components.Add(key.Name);
            }
        }


        public int Count => items.Count;
        public void Dispose()
        {
            OnQuantityChange = null;
            items.Clear();
            components.Clear();
            inventoryComponent.items.Remove(this);
        }
    }
}