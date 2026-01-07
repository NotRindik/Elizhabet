using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
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

        public override void Initialize(AbstractEntity owner)
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

            var itemStackIndexFrom = _inventoryComponent.items.Raw.FindIndex(c => toData.Item == c);
            var activeIndexBefore = _inventoryComponent.CurrentActiveIndex;
            var activeItem = _inventoryComponent.ActiveItem;

            if (from.GetItem() != null)
            {
                var fromData = from.GetItem().itemData;
                var itemStackIndexTo = _inventoryComponent.items.Raw.FindIndex(c => fromData.Item == c);
                _inventoryComponent.items.Swap(itemStackIndexFrom, itemStackIndexTo);
                SetActiveWeapon(activeIndexBefore);
                return;
            }

            int fromIndex = Mathf.Min(from.Index, to.Index);
            int toIndex = Mathf.Max(from.Index, to.Index);


            if (to.Index <= 4)
            {
                var temp = to.GetItem().itemData;
                var i = _inventoryComponent.items.Raw.FindIndex(a => temp.Item == a);
                _inventoryComponent.items.MoveItem(i, to.Index);
                Debug.Log("Moved");
            }
            else
            {
                Debug.Log("AddedTo");
                var temp = to.GetItem().itemData;
                var i = _inventoryComponent.items.Raw.FindIndex(a => temp.Item == a);
                _inventoryComponent.items.Raw[i] = null;
                _inventoryComponent.items.Raw.Add(temp.Item);
            }

            if (to.Index > 4)
            {
                if (activeIndexBefore == from.Index)
                {
                    SetActiveWeapon(activeIndexBefore - 1);


                    _inventoryComponent.ItemsLog.Clear();


                    for (int i = 0; i < _inventoryComponent.items.Count; i++)
                    {
                        _inventoryComponent.ItemsLog.Add(_inventoryComponent.items[i] != null ? _inventoryComponent.items[i].itemName.ToString() : null);
                    }
                    return;
                }
            }


            if(activeItem == null)
            {
                SetActiveWeapon(activeIndexBefore - 1);
            }

            _inventoryComponent.ItemsLog.Clear();
            for (int i = 0; i < _inventoryComponent.items.Count; i++)
            {
                _inventoryComponent.ItemsLog.Add(_inventoryComponent.items[i] != null ? _inventoryComponent.items[i].itemName.ToString() : null);
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
                    if (col.TryGetComponent(out AbstractEntity itemController))
                    {
                        if(itemController is ITakeAbleSystem takeAble)
                            return !takeAble.isSelected;
                        else
                        {
                            TakeAbleSystem takeAbleSys = itemController.GetControllerSystem<TakeAbleSystem>();
                            if(takeAbleSys != null) 
                                return takeAbleSys.isSelected;
                        }
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
                    if (existStack == null)
                        break;
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
/*
                var armourCongretion = _inventoryComponent.items.Raw
                    .Where(armourItem =>
                    {
                        var armour = armourItem.GetItemComponent<ArmourItemComponent>();
                        return armour != null && armour.isEquiped;
                    })
                    .Count();
*/
                bool isFindFreeElement = false;

                for (int i = 0; i < _inventoryComponent.items.Count; i++)
                {
                    if (_inventoryComponent.items[i] == null)
                    {
                        _inventoryComponent.items.Set(i,stack);
                        isFindFreeElement = true;
                        break;
                    }
                }

                if(!isFindFreeElement) 
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

            _inventoryComponent.ItemsLog.Clear();
            for (int i = 0; i < _inventoryComponent.items.Count; i++)
            {
                _inventoryComponent.ItemsLog.Add(_inventoryComponent.items[i] != null ? _inventoryComponent.items[i].itemName.ToString() : null);
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

                SetNearestItem(index, stack);
            }
        }

        private void SetNearestItem(int destroyedItem, ItemStack stack)
        {
            var list = _inventoryComponent.items.Raw;
            int count = list.Count;
            if (count == 0)
            {
                SetActiveWeaponWithoutDestroy(-1);
                return;
            }

            // Если stack есть в пределах разрешённых слотов
            int actualIndex = list.FindIndex(s => ReferenceEquals(s, stack));
            if (actualIndex >= 0 && actualIndex <= 4)
            {
                SetActiveWeaponWithoutDestroy(actualIndex);
                return;
            }

            // ограничиваем destroyedItem до допустимого диапазона
            int start = Mathf.Clamp(destroyedItem, 0, Mathf.Min(count - 1, 4));

            int chosen = -1;

            // 1) ищем ближайший справа от start, но только в диапазоне [0..4]
            for (int i = start; i <= Mathf.Min(count - 1, 4); i++)
            {
                if (list[i] != null) { chosen = i; break; }
            }

            // 2) если не нашли, ищем слева от start, но тоже только [0..4]
            if (chosen == -1)
            {
                for (int i = start - 1; i >= 0; i--)
                {
                    if (list[i] != null) { chosen = i; break; }
                }
            }

            // если ничего нет — ставим -1
            SetActiveWeaponWithoutDestroy(chosen);
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
                }
                else
                {
                    SetNearestItem(_inventoryComponent.CurrentActiveIndex, stack);
                }
            }
        }

        public void ThrowItem(Vector2 dir, float powerN, float force, float torque)
        {
            if (_inventoryComponent.ActiveItem)
            {
                _inventoryComponent.ActiveItem.Throw(dir,force * powerN);
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                float spinsCount = Mathf.Min(2f, Mathf.Floor(powerN * 2f));
                float spins = spinsCount * 360f;
                if (owner.transform.localScale.x < 0)
                {
                    angle -= 180;
                }

                _inventoryComponent.ActiveItem.transform
                .DORotate(
                            new Vector3(0, 0, angle + spins),
                            0.4f,
                            RotateMode.FastBeyond360
                        )
                        .SetEase(Ease.OutCubic);

                var stack = _inventoryComponent.items[_inventoryComponent.CurrentActiveIndex];
                stack.RemoveItem(_inventoryComponent.ActiveItem.Components);
                _inventoryComponent.ActiveItem = null;
                if (_inventoryComponent.items.Raw.Contains(stack))
                {
                    SetActiveWeaponWithoutDestroy(_inventoryComponent.items.Raw.FindIndex(element => element.itemName == stack.itemName));
                }
                else
                {
                    SetNearestItem(_inventoryComponent.CurrentActiveIndex, stack);
                }
            }
        }
        public void NextItem()
        {
            int current = _inventoryComponent.CurrentActiveIndex;

            // ���� ��������� �������� ����
            for (int i = current + 1; i < 5; i++)
            {
                if (_inventoryComponent.items[i] != null)
                {
                    SetActiveWeapon(i);
                    return;
                }
            }
        }

        public void PreviousItem()
        {
            int current = _inventoryComponent.CurrentActiveIndex;

            // ���� ���������� �������� ����
            for (int i = current - 1; i >= 0; i--)
            {
                if (_inventoryComponent.items[i] != null)
                {
                    SetActiveWeapon(i);
                    return;
                }
            }
            SetActiveWeapon(-1);
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
        public LayerMask itemLayer;
        public int CurrentActiveIndex => ActiveItem != null
            ? items.Raw.FindIndex(stack =>
                stack != null && stack.itemName == _activeItem.itemComponent.itemPrefab.name)
            : -1;


        public ObservableList<ItemStack> items = new ObservableList<ItemStack>(5,null);
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
            inventoryComponent.items.RemoveAndSetDefault(this);
        }
    }
}

[System.Serializable]
public class ObservableList<T>
{
    private List<T> _list = new List<T>();

    [SerializeField] private List<T> _serializedFields = new List<T>();

    public Action<T> OnItemAdded;
    public Action<T> OnItemRemoved;
    public Action<T> OnItemChanged;

    public void Add(T item)
    {
        _list.Add(item);
        OnItemAdded?.Invoke(item);
        OnItemChanged?.Invoke(item);

        UpdateSerialization();
    }

    public void UpdateSerialization()
    {
        _serializedFields.Clear();
        foreach (var item in _list)
        {
            _serializedFields.Add(item);
        }
    }

    public void Set(int i, T item)
    {
        _list[i]   = item;
        OnItemAdded?.Invoke(item);
        OnItemChanged?.Invoke(item);

        UpdateSerialization();
    }
    public void Insert(int i, T item)
    {
        _list.Insert(i, item);
        OnItemAdded?.Invoke(item);
        OnItemChanged?.Invoke(item);

        UpdateSerialization();
    }
    public bool Remove(T item)
    {
        bool removed = _list.Remove(item);
        if (removed)
        {
            OnItemRemoved?.Invoke(item);
            OnItemChanged?.Invoke(item);
        }
        UpdateSerialization();
        return removed;
    }

    public void MoveItem(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _list.Count || toIndex < 0 || toIndex >= _list.Count || fromIndex == toIndex)
            return;

        T item = _list[fromIndex];

        _list.RemoveAt(fromIndex);

        // ���� ���������� ����� �� ������, ����� ������ �����
        if (toIndex > fromIndex) toIndex--;

        _list.Insert(toIndex, item);
        UpdateSerialization();
    }

    public bool AreEqual(T a, T b)
    {
        return EqualityComparer<T>.Default.Equals(a, b);
    }
    public bool RemoveAndSetDefault(T item)
    {
        var removedIndex = _list.FindIndex(a => AreEqual(a,item));

        if (removedIndex != -1)
        {
            Raw[removedIndex] = default;
            OnItemRemoved?.Invoke(item);
            OnItemChanged?.Invoke(item);
        }
        UpdateSerialization();
        return removedIndex != -1;
    }
    public ObservableList(int defaultSize = 0, T defaultValue = default)
    {
        _list = new List<T>(defaultSize);
        for (int i = 0; i < defaultSize; i++)
            Add(defaultValue);
    }

    public void Swap(int indexA, int indexB)
    {
        (Raw[indexA], Raw[indexB]) = (Raw[indexB], Raw[indexA]);
        UpdateSerialization();
    }
    public T this[int index] => _list[index];
    public int Count => _list.Count;
    public List<T> Raw => _list;

    public void Clear()
    {
        for (int i = _list.Count - 1; i >= 0; i--)
        {
            Remove(_list[i]);
        }
        UpdateSerialization();
    }

    public void AssignFrom(List<T> other)
    {
        for (int i = _list.Count - 1; i >= 0; i--)
        {
            if (!other.Contains(_list[i]))
                Remove(_list[i]);
        }

        //�������� � ��� ��� ��� ��������� ��������� � ����� � ���������� ��� ���������� ������ 5 ��
        foreach (var item in other)
        {
            if (!_list.Contains(item) && item != null)
                Add(item);
        }

        UpdateSerialization();
    }


    public void SetRawSilently(IEnumerable<T> other)
    {
        _list.Clear();
        _list.AddRange(other);
        UpdateSerialization();
    }
}
