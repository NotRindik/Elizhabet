using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Controllers;
using std;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;

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

            mono.StartCoroutine(
                std.Utilities.Invoke(
                    () =>
                    {
                        var module = SaveManager.Instance.GetModule<GlobalSaves>();
                        module.onGlobalStateChange += OnGlobalStateChange;
                        if(module.Exist("StorageSize"))
                            OnGlobalStateChange("StorageSize", module.GetData("StorageSize"));
                    },
                    0.1f
                )
            );
        }

        public void OnGlobalStateChange(string key, string value)
        {
            if(key == "StorageSize")
            {
                _inventoryComponent.storage.limit = int.Parse(value);
            }
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
        
        public void SwapOrMoveItems(SlotRef from, SlotRef to)
        {
            int activeIndexBefore = _inventoryComponent.CurrentActiveIndex;

            bool fromIsStorage = ReferenceEquals(from.List, _inventoryComponent.storage.observableList);
            bool toIsStorage = ReferenceEquals(to.List, _inventoryComponent.storage.observableList);
            bool targetCellExists = to.Index < to.List.Count;

            if (!targetCellExists)
            {
                var movedStack = from.Value;

                if (fromIsStorage)
                    from.List.Remove(movedStack);
                else
                    from.Value = null;

                to.List.Add(movedStack);
            }
            else
            {
                var fromValue = from.Value;
                var toValue = to.Value;

                if (fromIsStorage && !toIsStorage && toValue == null)
                {
                    to.Value = fromValue;
                    from.List.Remove(fromValue);
                }
                else
                {
                    from.Value = toValue;
                    to.Value = fromValue;
                }
            }

            if (activeIndexBefore != -1)
                SetActiveWeapon(_inventoryComponent.CurrentActiveIndex);
        }

        public void SetItem(Item item)
        {
            if (item == null)
                return;
            
            if (TryAddToExistingStack(item))
                return;
            
            var newStack = CreateStack(item);
            if (AddStackToInventory(newStack))
            {
                HandleActiveItem(item,newStack);
            }
        }
        
        private bool TryAddToExistingStack(Item item)
        {
            foreach (var stack in _inventoryComponent.AllSlotsFlat())
            {
                if (stack == null)
                    continue;

                if (stack.itemName == item.itemComponent.itemPrefab.name)
                {
                    if (stack.IsFull)
                    {
                        continue;
                    }

                    stack.AddItem(item.Components.Where(pair => pair.Value is ISaveSerialize)
                        .ToDictionary(
                            pair => pair.Key,
                            pair => (ISaveSerialize)pair.Value
                        ));
                    SetActiveWeapon(_inventoryComponent.CurrentActiveIndex);
                    Object.Destroy(item.gameObject);
                    return true;
                }
            }

            return false;
        }
        private ItemStack CreateStack(Item item)
        {
            var stack = new ItemStack(item.itemComponent.itemPrefab.name, _inventoryComponent,item.itemComponent.stackSize);
            stack.AddItem(item.Components.Where(pair => pair.Value is ISaveSerialize)
                .ToDictionary(
                    pair => pair.Key,
                    pair => (ISaveSerialize)pair.Value
                ));
            
            return stack;
        }
        
        private bool AddStackToInventory(ItemStack stack)
        {
            var fixedSlotLists = new[] { _inventoryComponent.hotBar,_inventoryComponent.armor,_inventoryComponent.accessories };

            foreach (var list in fixedSlotLists)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] == null)
                    {
                        list.Set(i, stack);
                        return true;
                    }
                }
            }
            
            if (_inventoryComponent.storage.TryAdd(stack))
                return true;
            
            NotflicationManager.Instance.Send("Inventory Full");
            return false;
        }
        private void HandleActiveItem(Item item, ItemStack stack)
        {
            if (_inventoryComponent.ActiveItem == null)
            {
                item.SelectItem(_owner);
                _inventoryComponent.ActiveItem = item;
                _inventoryComponent.ActiveStack = stack;
                item.itemComponent.currentOwner = _owner;
                Object.DontDestroyOnLoad(item);
            }
            else
            {
                Object.Destroy(item.gameObject);
            }
        }
        
        public bool CanAcceptItem(string itemName)
        {
            foreach (var stack in _inventoryComponent.AllSlotsFlat())
            {
                if (stack != null && stack.itemName == itemName && !stack.IsFull)
                    return true;
            }
            
            for (int i = 0; i < _inventoryComponent.hotBar.Count; i++)
            {
                if (_inventoryComponent.hotBar[i] == null)
                    return true;
            }
            
            if (!_inventoryComponent.storage.IsFull)
                return true;

            return false;
        }
        
        public void OnItemDestroy(AbstractEntity entity)
        {
            if (entity is Item item)
            {
                if (item.GetControllerComponent<HealthComponent>().currHealth > 0)
                {
                    return;
                }
                int index = _inventoryComponent.AllSlotsFlat().ToList().FindIndex(itemStack => itemStack.itemName == item.itemComponent.itemPrefab.name);
                var stack = _inventoryComponent.AllSlotsFlat().ToList()[index];

                stack.RemoveItem(item);

                SetNearestItem(index, stack);
            }
        }

        private void SetNearestItem(int destroyedItem, ItemStack stack)
        {
            var list = _inventoryComponent.hotBar.Raw;
            int count = list.Count;
            if (count == 0)
            {
                SetActiveWeaponWithoutDestroy(-1);
                return;
            }
            
            int actualIndex = list.FindIndex(s => ReferenceEquals(s, stack));
            if (actualIndex >= 0 && actualIndex <= 4)
            {
                SetActiveWeaponWithoutDestroy(actualIndex);
                return;
            }
            
            int start = Mathf.Clamp(destroyedItem, 0, Mathf.Min(count - 1, list.Count-1));

            int chosen = -1;
            
            for (int i = start; i <= Mathf.Min(count - 1, list.Count-1); i++)
            {
                if (list[i] != null) { chosen = i; break; }
            }
            
            if (chosen == -1)
            {
                for (int i = start - 1; i >= 0; i--)
                {
                    if (list[i] != null) { chosen = i; break; }
                }
            }
            
            SetActiveWeaponWithoutDestroy(chosen);
        }




        public void ThrowItem()
        {
            if (_inventoryComponent.ActiveItem)
            {
                _inventoryComponent.ActiveItem.Throw();
                SceneManager.MoveGameObjectToScene(_inventoryComponent.ActiveItem.gameObject,SceneLoader.SceneFlow.CurrentScene);
                var stack = _inventoryComponent.hotBar[_inventoryComponent.CurrentActiveIndex];
                stack.RemoveItem(_inventoryComponent.ActiveItem);
                _inventoryComponent.ActiveItem = null;
                if (_inventoryComponent.hotBar.Raw.Contains(stack))
                {
                    SetActiveWeaponWithoutDestroy(_inventoryComponent.hotBar.Raw.FindIndex(element => element.itemName == stack.itemName));
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
                SceneManager.MoveGameObjectToScene(_inventoryComponent.ActiveItem.gameObject, SceneLoader.SceneFlow.CurrentScene);
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

                var stack = _inventoryComponent.hotBar[_inventoryComponent.CurrentActiveIndex];
                stack.RemoveItem(_inventoryComponent.ActiveItem);
                _inventoryComponent.ActiveItem = null;
                if (_inventoryComponent.hotBar.Raw.Contains(stack))
                {
                    SetActiveWeaponWithoutDestroy(_inventoryComponent.hotBar.Raw.FindIndex(element => element.itemName == stack.itemName));
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
            
            for (int i = current + 1; i < 5; i++)
            {
                if (_inventoryComponent.hotBar[i] != null)
                {
                    if (_inventoryComponent.hotBar[i].Count == 0)
                        continue;
                    SetActiveWeapon(i);
                    return;
                }
            }
        }

        public void PreviousItem()
        {
            int current = _inventoryComponent.CurrentActiveIndex;
            
            for (int i = current - 1; i >= 0; i--)
            {
                if (_inventoryComponent.hotBar[i] != null)
                {
                    if (_inventoryComponent.hotBar[i].Count == 0)
                        continue;
                    SetActiveWeapon(i);
                    return;
                }
            }
            SetActiveWeapon(-1);
        }

        public override void OnEnable()
        {
            base.OnEnable();
            _inventoryComponent?.ActiveItem?.gameObject?.SetActive(true);
        }

        public override void OnDisable()
        {
            base.OnDisable();
            _inventoryComponent?.ActiveItem?.gameObject?.SetActive(false);
        }


        private void SetActiveWeapon(int index)
        {
            Object.DestroyImmediate(_inventoryComponent.ActiveItem?.gameObject);
            SetActiveWeaponWithoutDestroy(index);
        }
        private void SetActiveWeaponWithoutDestroy(int index)
        {
            if (index > -1)
            {
                ItemStack hotbarStack = _inventoryComponent.hotBar[index];
                
                if(hotbarStack == null)
                {
                    _inventoryComponent.ActiveStack = null;
                    _inventoryComponent.ActiveItem = null;
                    return;
                }
                
                GameObject inst = Object.Instantiate(GameResourcesManager.Instance.ItemsDataBase.Get(hotbarStack.itemName).gameObject);
                var item = inst.GetComponent<Item>();
                Object.DontDestroyOnLoad(inst);
                item.InitAfterSpawnFromInventory(_inventoryComponent.hotBar[index].items[0]);
                
                _inventoryComponent.ActiveStack = _inventoryComponent.hotBar[index];
                
                _inventoryComponent.ActiveItem = item;
                _inventoryComponent.ActiveItem.SelectItem(_owner);
                _inventoryComponent.ActiveItem.itemComponent.currentOwner = _owner;
            }
            else
            {
                _inventoryComponent.ActiveStack = null;
                _inventoryComponent.ActiveItem = null;
            }
        }
        public void Dispose()
        {
            SetActiveWeapon(-1);
            _inventoryComponent.OnActiveItemChange -= OnActiveItemChange;
        }
    }
    

    [System.Serializable]
    public class InventoryComponent: IComponent
    {
        public int CurrentActiveIndex => ActiveItem != null
            ? hotBar.Raw.FindIndex(stack => stack != null && stack.GetItemComponent<ItemComponent>() == _activeItem.itemComponent)
            : -1;
        public IEnumerable<ItemStack> AllSlotsFlat() =>
            hotBar.Raw.Concat(storage.Raw).Concat(armor.Raw).Concat(accessories.Raw);
        
        public IEnumerable<ItemStack> HotBarAndStorage() =>
            hotBar.Raw.Concat(storage.Raw);
        
        [NonSerialized] public ObservableList<ItemStack> hotBar = new ObservableList<ItemStack>(6, null);
        [NonSerialized] public BoundedObservableList<ItemStack> storage = new BoundedObservableList<ItemStack>();
        [NonSerialized] public ObservableList<ItemStack> armor = new ObservableList<ItemStack>(6, null);
        [NonSerialized] public ObservableList<ItemStack> accessories = new ObservableList<ItemStack>(3, null);
        
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
        
        public delegate void ActiveStackChangedHandler(ItemStack current, ItemStack previous);
        public event ActiveStackChangedHandler OnActiveStackChange;

        private ItemStack _activeStack;

        public ItemStack ActiveStack
        {
            get => _activeStack;
            set
            {
                var previous = _activeStack;
                _activeStack = value;
                OnActiveStackChange?.Invoke(_activeStack, previous);
            }
        }
        
        
        public bool RemoveItemAnywhere(ItemStack item)
        {
            return hotBar.RemoveAndSetDefault(item)
                   || storage.RemoveAndSetDefault(item)
                   || armor.RemoveAndSetDefault(item)
                   || accessories.RemoveAndSetDefault(item);
        }
        public bool RemoveItemAnywhereSilent(ItemStack item)
        {
            return hotBar.RemoveAndSetDefaultSilent(item)
                   || storage.RemoveAndSetDefaultSilent(item)
                   || armor.RemoveAndSetDefaultSilent(item)
                   || accessories.RemoveAndSetDefaultSilent(item);
        }

        
        public SlotRef GetSlotRef(int flatIndex)
        {
            if (flatIndex < hotBar.Count)
                return new SlotRef(hotBar, flatIndex);
            flatIndex -= hotBar.Count;

            if (flatIndex < armor.Count)
                return new SlotRef(armor, flatIndex);
            flatIndex -= armor.Count;

            if (flatIndex < accessories.Count)
                return new SlotRef(accessories, flatIndex);
            flatIndex -= accessories.Count;

            return new SlotRef(storage.observableList, flatIndex);
        }
        
        public void Swap(SlotRef first, SlotRef second)
        {
            if(first == second)
                return;
            
            (first.Value, second.Value) = (second.Value, first.Value);
        }
    }
    
    public struct SlotRef
    {
        public ObservableList<ItemStack> List;
        public int Index;

        public SlotRef(ObservableList<ItemStack> list, int index)
        {
            List = list;
            Index = index;
        }

        public ItemStack Value
        {
            get => List[Index];
            set => List.Set(Index, value);
        }
        
        public static bool operator ==(SlotRef a, SlotRef b)
        {
            bool res = false;
            res = a.List == b.List;
            
            if(res)
                res = a.Index == b.Index;
            
            return res;
        }
        
        public static bool operator !=(SlotRef a, SlotRef b)
        {
            bool res = false;
            res = a.List == b.List;
            
            if(res)
                res = a.Index == b.Index;
            
            return res;
        }
    }
    
    [System.Serializable]
    public class ItemStack:IDisposable
    {
        public string itemName;
        [HideInInspector] public InventoryComponent inventoryComponent;

        public List<Dictionary<Type, ISaveSerialize>> items;
        


        public List<string> components = new List<string>();
        public int count;
        public event Action<int> OnQuantityChange;

        public int maxStackSize = 1;

        public bool IsFull => Count >= maxStackSize;

        public ItemStack(string name, InventoryComponent inventoryComponent,int maxStackSize = 1)
        {
            itemName = name;
            this.inventoryComponent = inventoryComponent;

            this.maxStackSize = maxStackSize;

            OnQuantityChange += count =>
            {
                if (count == 0)
                    Dispose();
            };
            items = new List<Dictionary<Type, ISaveSerialize>>();
            OnQuantityChange += c => UpdateComponentSerialization();
        }
        public T GetItemComponent<T>() where T : IComponent
        {
            items[0].TryGetValue(typeof(T), out var itemComp);
            // if (itemComp == null)
            //     itemComp = GetItemComponentFromConfig<T>();
            
            return (T)itemComp;
        }
        
        /// <summary>
        /// Кароче это доступ лишь к данным из префаба изменения данных оттуда просто не допустимы там данные readOnly
        /// </summary>
        /// <typeparam name="T">IComponent</typeparam>
        /// <returns>IComponent</returns>
        public T GetItemComponentFromConfig<T>() where T : IComponent
        {
            return (T)GameResourcesManager.Instance.ItemsDataBase.Get(itemName).GetControllerComponentDirect<T>();
        }
        public void AddItem(Dictionary<Type, ISaveSerialize> item)
        {
            items.Add(item);
            OnQuantityChange?.Invoke(Count);
        }

        public void RemoveItem(Item item)
        {
            var index = items.FindIndex(dict =>
                dict.TryGetValue(typeof(ItemComponent), out var component) &&
                ReferenceEquals(component, item.itemComponent));

            if (index != -1)
                items.RemoveAt(index);
            
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
            inventoryComponent.RemoveItemAnywhere(this);
        }   
    }
}