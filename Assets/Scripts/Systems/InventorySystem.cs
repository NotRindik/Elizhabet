using System;
using System.Collections.Generic;
using System.Linq;
using Common;
using Controllers;
using UnityEngine;
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
        private void OnActiveItemChange(Item curr,Item past)
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
        public void SwapItems(SlotBase from, SlotBase to, SlotBase[] inventorySlots)
        {
            var toData = to.GetItem().itemData;
            
            var itemStackIndexFrom = _inventoryComponent.items.Raw.FindIndex(c => toData == c);
            var activeIndexBefore = _inventoryComponent.CurrentActiveIndex;
            
            if (from.GetItem() != null)
            {
                var fromData = from.GetItem().itemData;
                var itemStackIndexTo = _inventoryComponent.items.Raw.FindIndex(c => fromData == c);
                _inventoryComponent.items.Swap(itemStackIndexFrom,itemStackIndexTo);
                SetActiveWeapon(activeIndexBefore);
                return;
            }
            
            int fromIndex = Mathf.Min(from.Index, to.Index);
            int toIndex = Mathf.Max(from.Index, to.Index);

            int beetweenItemQuantity = 0;
            for (int i = fromIndex+1; i < toIndex; i++)
            {
                if (inventorySlots[i].GetItem() != null)
                {
                    beetweenItemQuantity++;
                }
            }
            
            _inventoryComponent.hotSlotCongestion = 0;
            for (int i = 0; i < 5; i++)
            {
                if (inventorySlots[i].GetItem() != null)
                {
                    _inventoryComponent.hotSlotCongestion++;
                }
            }
            
            if (beetweenItemQuantity == 0 && to.Index < 4)
                return;
            
            int index = itemStackIndexFrom + (beetweenItemQuantity * (from.Index > to.Index ? -1 : 1));
            
            ItemStack temp = _inventoryComponent.items[itemStackIndexFrom];
            _inventoryComponent.items.Raw.RemoveAt(itemStackIndexFrom);

            if (beetweenItemQuantity >= _inventoryComponent.items.Count && from.Index < to.Index)
            {
                _inventoryComponent.items.Raw.Add(temp);
            }
            else
            {

                _inventoryComponent.items.Raw.Insert(index, temp);
            }
            if (to.Index > 4)
            {
                if (activeIndexBefore == from.Index || _inventoryComponent.CurrentActiveIndex + 1 >= _inventoryComponent.hotSlotCongestion)
                {
                    SetActiveWeapon(activeIndexBefore - 1);
                    _inventoryComponent.ItemsLog.Clear();
                    for (int i = 0; i < _inventoryComponent.items.Count; i++)
                    {
                        _inventoryComponent.ItemsLog.Add(_inventoryComponent.items[i].itemName.ToString());
                    }
                    return;
                }
            }
            SetActiveWeapon(activeIndexBefore);

            _inventoryComponent.ItemsLog.Clear();
            for (int i = 0; i < _inventoryComponent.items.Count; i++)
            {
                _inventoryComponent.ItemsLog.Add(_inventoryComponent.items[i].itemName.ToString());
            }
        }



        public void TakeItem() 
        {
            Collider2D nearestItem = Physics2D.OverlapCircleAll(
                    _owner.transform.position,
                    _inventoryComponent.itemCheckRadius,
                    _inventoryComponent.itemLayer)
                .Where(col =>
                {
                    if (col.TryGetComponent(out Item itemController))
                    {
                        return !itemController.isSelected;
                    }
                    return false; 
                })
                .OrderBy(col => Vector2.Distance(col.transform.position, _owner.transform.position))
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
                        SetActiveWeapon(_inventoryComponent.CurrentActiveIndex);
                        Object.Destroy(item.gameObject);
                        return;
                    }
                }


                var stack = new ItemStack(item.itemComponent.itemPrefab.name,_inventoryComponent);
                

                stack.AddItem(item.Components);
                if(_inventoryComponent.hotSlotCongestion < 5) _inventoryComponent.items.Insert(_inventoryComponent.hotSlotCongestion, stack);
                else _inventoryComponent.items.Add(stack);

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

            _inventoryComponent.ItemsLog.Clear();
            for (int i = 0; i < _inventoryComponent.items.Count; i++)
            {
                _inventoryComponent.ItemsLog.Add(_inventoryComponent.items[i].itemName.ToString());
            }
        }
        public void OnItemDestroy(EntityController entity)
        {
            if (entity is Item item)
            {
                if (item.healthComponent.currHealth > 0)
                {
                    return;
                }
                int index = _inventoryComponent.items.Raw.FindIndex(itemStack => itemStack.itemName == item.itemComponent.itemPrefab.name);
                var stack = _inventoryComponent.items[index];
        
                stack.RemoveItem(item.Components);
        
                if (!_inventoryComponent.items.Raw.Contains(stack))
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
        }

        public void ThrowItem()
        {
            if (_inventoryComponent.ActiveItem)
            {
                _inventoryComponent.ActiveItem.Throw();
                var stack = _inventoryComponent.items[_inventoryComponent.CurrentActiveIndex];
                stack.RemoveItem(_inventoryComponent.ActiveItem.Components);
                _inventoryComponent.ActiveItem = null;
                if (_inventoryComponent.items.Raw.Contains(stack))
                {
                    SetActiveWeaponWithoutDestroy(_inventoryComponent.items.Raw.FindIndex(element => element.itemName == stack.itemName));
                    _inventoryComponent.hotSlotCongestion--;
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
            if (_inventoryComponent.CurrentActiveIndex + 1 >= _inventoryComponent.hotSlotCongestion)
            {
                return;
            }
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
                GameObject inst = Object.Instantiate(((ItemComponent)_inventoryComponent.items[index].items[0][typeof(ItemComponent)]).itemPrefab);
                var item = inst.GetComponent<Item>();
                item.InitAfterSpawnFromInventory(_inventoryComponent.items[index].items[0]);
                _inventoryComponent.items[index].items[0] = item.Components;
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
        public int hotSlotCongestion;
        public LayerMask itemLayer;
        public int CurrentActiveIndex => ActiveItem != null ?items.Raw.FindIndex(stack =>
            {
                return stack.itemName == _activeItem.itemComponent.itemPrefab.name;
            }
        ) : -1;

        public ObservableList<ItemStack> items = new ObservableList<ItemStack>();
        public List<string> ItemsLog = new List<string>();
        
        public delegate void ActiveItemChangedHandler(Item current, Item previous);
        public event ActiveItemChangedHandler OnActiveItemChange;

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
        public T GetItemComponent<T>() where T : IComponent
        {
            items[0].TryGetValue(typeof(T), out var itemComp);
            return (T)itemComp;
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
            SortByDurability();
            count = Count;
            if (Count == 0)
                return;
            components.Clear();
            foreach (var key in items[0].Keys)
            {
                components.Add(key.Name);
            }
        }
        private void SortByDurability()
        {

            items = items
                .OrderBy(component =>
                {
                    HealthComponent healthComponent = (HealthComponent)component[typeof(HealthComponent)];
                    return healthComponent != null ? healthComponent.currHealth : int.MaxValue;
                })
                .ToList();
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


public class ObservableList<T>
{
    private readonly List<T> _list = new List<T>();

    public Action<T> OnItemAdded;
    public Action<T> OnItemRemoved;
    public Action<T> OnItemChanged;

    public void Add(T item)
    {
        _list.Add(item);
        OnItemAdded?.Invoke(item);
        OnItemChanged?.Invoke(item);
    }
    public void Insert(int i, T item)
    {
        _list.Insert(i, item);
        OnItemAdded?.Invoke(item);
        OnItemChanged?.Invoke(item);
    }
    public bool Remove(T item)
    {
        bool removed = _list.Remove(item);
        if (removed)
        {
            OnItemRemoved?.Invoke(item);
            OnItemChanged?.Invoke(item);
        }
        return removed;
    }
    public void Swap(int indexA, int indexB)
    {
        (Raw[indexA], Raw[indexB]) = (Raw[indexB], Raw[indexA]);
    }
    public T this[int index] => _list[index];
    public int Count => _list.Count;
    public List<T> Raw => _list;
}
