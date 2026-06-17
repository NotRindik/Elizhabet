using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Controllers;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;

namespace Systems
{
    public enum ItemCategory { Weapon, Consumable, Apparel, Modifier, Material }
    public enum EquipSlot    { Head, Chest, Legs, Hands, Accessory }
    
    public class InventorySystem : BaseSystem, IDisposable
    {
        private InventoryComponent _inv;
        private EntityController _owner;

        // ═══════════════════════════════════════════════════
        // INIT
        // ═══════════════════════════════════════════════════

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _owner = (EntityController)owner;
            _inv = _owner.GetControllerComponent<InventoryComponent>();

            _inv.hotBar.slots = new ItemStack[_inv.hotBar.capacity];
            _inv.OnActiveItemChange += OnActiveItemChange;

            mono.StartCoroutine(std.Utilities.Invoke(() =>
            {
                var module = SaveManager.Instance.GetModule<GlobalSaves>();
                module.onGlobalStateChange += OnGlobalStateChange;

                if (module.Exist("InvStackSize"))
                    OnGlobalStateChange("InvStackSize", module.GetData("InvStackSize"));
                if (module.Exist("HotBarSize"))
                    OnGlobalStateChange("HotBarSize", module.GetData("HotBarSize"));
            }, 0.1f));
        }

        public void OnGlobalStateChange(string key, string value)
        {
            if (key == "InvStackSize")
            {
                int size = int.Parse(value);
                foreach (var stack in _inv.hotBar.slots)
                    if (stack != null) stack.maxStackSize = size;
                foreach (var stack in _inv.storage.items)
                    if (stack != null) stack.maxStackSize = size;
            }
            if (key == "HotBarSize")
            {
                ResizeHotBar(int.Parse(value));
            }
        }

        private void ResizeHotBar(int newCapacity)
        {
            var old = _inv.hotBar.slots;
            _inv.hotBar.slots = new ItemStack[newCapacity];
            int copyCount = Mathf.Min(old.Length, newCapacity);
            for (int i = 0; i < copyCount; i++)
                _inv.hotBar.slots[i] = old[i];
            for (int i = newCapacity; i < old.Length; i++)
                if (old[i] != null) _inv.storage.items.Add(old[i]);
            _inv.hotBar.capacity = newCapacity;
            _inv.hotBar.NotifyChanged(); // ←
            _inv.storage.NotifyChanged(); // ← стаки могли уехать в storage
        }

        // ═══════════════════════════════════════════════════
        // PICKUP — оружие → хотбар, остальное → storage
        // ═══════════════════════════════════════════════════

        public void PickupItem(Item item)
        {
            if (item == null) return;

            string itemName = item.itemComponent.itemPrefab.name;
            var existing = FindExistingStack(itemName);

            if (existing != null)
            {
                if (existing.IsFull)
                {
                    NotflicationManager.Instance.Send("Stack Full");
                    return;
                }

                existing.AddItem(item.Components);
                Object.Destroy(item.gameObject);

                if (existing == _inv.ActiveStack)
                    RefreshActiveItem();

                _inv.hotBar.NotifyChanged();
                _inv.storage.NotifyChanged();
                return;
            }

            if (item.itemComponent.category == ItemCategory.Weapon && TryAddNewToHotBar(item))
                return;

            AddNewToStorage(item);
        }

        private ItemStack FindExistingStack(string itemName)
        {
            foreach (var stack in _inv.hotBar.slots)
                if (stack != null && stack.itemName == itemName) return stack;

            foreach (var stack in _inv.storage.items)
                if (stack != null && stack.itemName == itemName) return stack;

            return null;
        }
        
        private bool TryAddNewToHotBar(Item item)
        {
            for (int i = 0; i < _inv.hotBar.slots.Length; i++)
            {
                if (_inv.hotBar.slots[i] == null)
                {
                    var stack = CreateStack(item);
                    _inv.hotBar.slots[i] = stack;
                    if (_inv.ActiveItem == null)
                        SetActiveSlotWithExistingItem(i, item);
                    else
                        Object.Destroy(item.gameObject);
                    _inv.hotBar.NotifyChanged();
                    return true;
                }
            }
            return false;
        }

        private void AddNewToStorage(Item item)
        {
            var newStack = CreateStack(item);
            _inv.storage.items.Add(newStack);
            Object.Destroy(item.gameObject);
            _inv.storage.NotifyChanged();
        }

        
        private void SetActiveSlotWithExistingItem(int index, Item existingItem)
        {
            Object.DestroyImmediate(_inv.ActiveItem?.gameObject);
            _inv.hotBar.activeIndex = index;

            Object.DontDestroyOnLoad(existingItem.gameObject);
            existingItem.SelectItem(_owner);
            existingItem.itemComponent.currentOwner = _owner;
            _inv.ActiveItem = existingItem;
        }

        private ItemStack CreateStack(Item item)
        {
            var stack = new ItemStack(
                item.itemComponent.itemPrefab.name,
                item.itemComponent.category,
                _inv
            );
            stack.AddItem(item.Components);
            return stack;
        }

        // ═══════════════════════════════════════════════════
        // MOVE / SWAP — один метод для UI
        // ═══════════════════════════════════════════════════

        // Оба слота описываем одним типом чтобы не плодить перегрузки

        public void MoveOrSwap(SlotRef from, SlotRef to)
        {
            var stackFrom = GetStack(from);
            var stackTo   = GetStack(to);
            if (stackFrom == null) return;
            
            
            // Нельзя свапать предметы разных категорий между хотбаром и storage
            if (stackTo != null && !from.IsHotBar && to.IsHotBar ||
                stackTo != null && from.IsHotBar && !to.IsHotBar)
            {
                if (stackFrom.category != stackTo.category)
                {
                    NotflicationManager.Instance.Send("Can't swap different item types");
                    return;
                }
            }

            if (stackTo == null)
            {
                SetStack(from, null);
                SetStack(to, stackFrom);
            }
            else
            {
                SetStack(from, stackTo);
                SetStack(to, stackFrom); 
            }

            int active = _inv.hotBar.activeIndex;
            if (from.IsHotBar && from.Index == active || to.IsHotBar   && to.Index   == active)
            {
                RefreshActiveItem();
            }
            
            OnSwapped?.Invoke(from, to);
        }

        public event Action<SlotRef, SlotRef> OnSwapped;
        
        public readonly struct SlotRef
        {
            public readonly bool IsHotBar;
            public readonly int  Index;

            public SlotRef(bool isHotBar, int index)
            {
                IsHotBar = isHotBar;
                Index    = index;
            }
        }

        private ItemStack GetStack(SlotRef slot)
        {
            if (slot.IsHotBar) return _inv.hotBar.slots[slot.Index];
            return slot.Index < _inv.storage.items.Count ? _inv.storage.items[slot.Index] : null;
        }

        private void SetStack(SlotRef slot, ItemStack stack)
        {
            if (slot.IsHotBar)
            {
                _inv.hotBar.slots[slot.Index] = stack;
            }
            else
            {
                while (_inv.storage.items.Count <= slot.Index)
                    _inv.storage.items.Add(null);
                _inv.storage.items[slot.Index] = stack;
            }
        }

        // ═══════════════════════════════════════════════════
        // HOTBAR NAVIGATION
        // ═══════════════════════════════════════════════════

        public void NextItem()
        {
            int current = _inv.hotBar.activeIndex;
            for (int i = current + 1; i < _inv.hotBar.capacity; i++)
            {
                if (_inv.hotBar.slots[i] is { Count: > 0 })
                {
                    SetActiveSlot(i);
                    return;
                }
            }
        }

        public void PreviousItem()
        {
            int current = _inv.hotBar.activeIndex;
            for (int i = current - 1; i >= 0; i--)
            {
                if (_inv.hotBar.slots[i] is { Count: > 0 })
                {
                    SetActiveSlot(i);
                    return;
                }
            }
            SetActiveSlot(-1);
        }

        // ═══════════════════════════════════════════════════
        // THROW
        // ═══════════════════════════════════════════════════

        public void ThrowItem()
        {
            if (_inv.ActiveItem == null) return;
            var stack = _inv.hotBar.slots[_inv.hotBar.activeIndex];
            SceneManager.MoveGameObjectToScene(_inv.ActiveItem.gameObject, SceneLoader.SceneFlow.CurrentScene);
            _inv.ActiveItem.Throw();
            int activeIndex = _inv.hotBar.activeIndex;
            stack.RemoveItem(_inv.ActiveItem.Components);
            _inv.ActiveItem = null;
            SetNearestActiveSlot(activeIndex);
            _inv.hotBar.NotifyChanged(); // ←
        }

        public void ThrowItem(Vector2 dir, float powerN, float force, float torque)
        {
            if (_inv.ActiveItem == null) return;
            var stack = _inv.hotBar.slots[_inv.hotBar.activeIndex];
            SceneManager.MoveGameObjectToScene(_inv.ActiveItem.gameObject, SceneLoader.SceneFlow.CurrentScene);
            _inv.ActiveItem.Throw(dir, force * powerN);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            float spins = Mathf.Min(2f, Mathf.Floor(powerN * 2f)) * 360f;
            if (_owner.transform.localScale.x < 0) angle -= 180;
            _inv.ActiveItem.transform
                .DORotate(new Vector3(0, 0, angle + spins), 0.4f, RotateMode.FastBeyond360)
                .SetEase(Ease.OutCubic);
            int activeIndex = _inv.hotBar.activeIndex; // ← фикс который был раньше
            stack.RemoveItem(_inv.ActiveItem.Components);
            _inv.ActiveItem = null;
            SetNearestActiveSlot(activeIndex);
            _inv.hotBar.NotifyChanged(); // ←
        }

        // ═══════════════════════════════════════════════════
        // ITEM DESTROYED (durability → 0)
        // ═══════════════════════════════════════════════════

        public void OnItemDestroy(EntityController entity)
        {
            if (entity is not Item item) return;
            if (item.healthComponent.currHealth > 0) return;
            int index = Array.FindIndex(_inv.hotBar.slots,
                s => s != null && s.items.Contains(item.Components));
            if (index == -1) return;
            _inv.hotBar.slots[index].RemoveItem(item.Components);
            SetNearestActiveSlot(index);
            _inv.hotBar.NotifyChanged(); // ←
        }

        // ═══════════════════════════════════════════════════
        // ACTIVE ITEM MANAGEMENT
        // ═══════════════════════════════════════════════════

        private void SetActiveSlot(int index)
        {
            Object.DestroyImmediate(_inv.ActiveItem?.gameObject);
            _inv.hotBar.activeIndex = index;
            SpawnActiveItem(index);
        }

        // Используется когда GameObject уже не нужно уничтожать (выброс, смерть итема)
        private void SetNearestActiveSlot(int fromIndex)
        {
            int chosen = -1;
            var slots = _inv.hotBar.slots;

            for (int i = fromIndex; i < slots.Length; i++)
                if (slots[i] is { Count: > 0 }) { chosen = i; break; }

            if (chosen == -1)
                for (int i = fromIndex - 1; i >= 0; i--)
                    if (slots[i] is { Count: > 0 }) { chosen = i; break; }

            _inv.hotBar.activeIndex = chosen;
            SpawnActiveItem(chosen);
        }

        private void RefreshActiveItem()
        {
            Object.DestroyImmediate(_inv.ActiveItem?.gameObject);
            SpawnActiveItem(_inv.hotBar.activeIndex);
        }

        private void SpawnActiveItem(int index)
        {
            if (index < 0 || _inv.hotBar.slots[index] == null || _inv.hotBar.slots[index].Count == 0)
            {
                _inv.ActiveItem = null;
                return;
            }

            var stack = _inv.hotBar.slots[index];
            var prefab = ((ItemComponent)stack.items[0][typeof(ItemComponent)]).itemPrefab;
            var inst   = Object.Instantiate(prefab);
            var item   = inst.GetComponent<Item>();

            Object.DontDestroyOnLoad(inst);
            item.InitAfterSpawnFromInventory(stack.items[0]);
            stack.items[0] = item.Components;

            _inv.ActiveItem = item;
            item.SelectItem(_owner);
            item.itemComponent.currentOwner = _owner;
        }

        private void OnActiveItemChange(Item curr, Item past)
        {
            if (past) past.OnRequestDestroy -= OnItemDestroy;
            if (curr) curr.OnRequestDestroy += OnItemDestroy;
        }

        // ═══════════════════════════════════════════════════
        // ENABLE / DISABLE / DISPOSE
        // ═══════════════════════════════════════════════════

        public override void OnEnable()
        {
            base.OnEnable();
            _inv?.ActiveItem?.gameObject?.SetActive(true);
        }

        public override void OnDisable()
        {
            base.OnDisable();
            _inv?.ActiveItem?.gameObject?.SetActive(false);
        }

        public void Dispose()
        {
            SetActiveSlot(-1);
            _inv.OnActiveItemChange -= OnActiveItemChange;
        }
    }

    
    public class HotBarData
    {
        public ItemStack[] slots;
        public int capacity   = 6;
        public int activeIndex = -1;

        public event Action OnChanged;
        public void NotifyChanged() => OnChanged?.Invoke();
    }

    public class StorageData
    {
        public List<ItemStack> items = new();

        public event Action OnChanged;
        public void NotifyChanged() => OnChanged?.Invoke();
    }

    [System.Serializable]
    public class InventoryComponent: IComponent
    {
        public HotBarData  hotBar  = new();
        public StorageData storage = new();
        
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
        
        public ItemStack ActiveStack => 
            hotBar.activeIndex >= 0 && hotBar.activeIndex < hotBar.slots.Length 
                ? hotBar.slots[hotBar.activeIndex] 
                : null;
        
    }
    
    
    [System.Serializable]
    public class ItemStack:IDisposable
    {
        public string itemName;
        [HideInInspector] public InventoryComponent inventoryComponent;

        public List<Dictionary<Type, IComponent>> items = new List<Dictionary<Type, IComponent>>();
        public ItemCategory category;
        public event Action<int> OnQuantityChange;

        public int maxStackSize = 1;

        public bool IsFull => Count >= maxStackSize;

        public ItemStack(string name,ItemCategory category, InventoryComponent inventoryComponent)
        {
            itemName = name;
            this.inventoryComponent = inventoryComponent;
            this.category = category;

            OnQuantityChange += count =>
            {
                if (count == 0)
                    Dispose();
            };
            items = new List<Dictionary<Type, IComponent>>();
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

            var slots = inventoryComponent.hotBar.slots;
            for (int i = 0; i < slots.Length; i++)
                if (ReferenceEquals(slots[i], this)) { slots[i] = null; break; }

            var storageItems = inventoryComponent.storage.items;
            int idx = storageItems.FindIndex(s => ReferenceEquals(s, this));
            if (idx != -1) storageItems[idx] = null;

            inventoryComponent.hotBar.NotifyChanged();   // ←
            inventoryComponent.storage.NotifyChanged();  // ←
        }
    }
}