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
                        
                        if(module.Exist("InvStackSize"))
                            OnGlobalStateChange("InvStackSize", module.GetData("InvStackSize"));
                        if(module.Exist("InvSize"))
                            OnGlobalStateChange("InvSize", module.GetData("InvSize"));
                    },
                    0.1f
                )
            );
        }

        public void OnGlobalStateChange(string key, string value)
        {
            if(key == "InvSize")
            {
                _inventoryComponent.inventorySize = int.Parse(value);
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
        
        public void SwapItems(SlotBase from, SlotBase to)
        {
            if (from.GetItem() == null || to.GetItem() == null)
                return;

            var fromStack = from.GetItem().itemData.Item;
            var toStack = to.GetItem().itemData.Item;

            int fromIndex = _inventoryComponent.AllSlotsFlat()
                .ToList()
                .FindIndex(x => ReferenceEquals(x, fromStack));

            int toIndex = _inventoryComponent.AllSlotsFlat()
                .ToList()
                .FindIndex(x => ReferenceEquals(x, toStack));

            if (fromIndex == -1 || toIndex == -1)
                return;

            int activeIndexBefore = _inventoryComponent.CurrentActiveIndex;

            _inventoryComponent.Swap(fromIndex, toIndex);

            SetActiveWeapon(activeIndexBefore);
        }

        public bool IsFullStack(Item item)
        {
            for (int i = 0; i < _inventoryComponent.AllSlotsFlat().ToList().Count; i++)
            {
                var stack = _inventoryComponent.AllSlotsFlat().ToList()[i];
                if (stack == null)
                    break;

                if (stack.IsFull && stack.itemName == item.itemComponent.itemPrefab.name)
                {
                    NotflicationManager.Instance.Send("Stack Full");
                    return true;
                }
            }

            return false;
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
                HandleActiveItem(item);
            }
        }
        
        private bool TryAddToExistingStack(Item item)
        {
            for (int i = 0; i < _inventoryComponent.AllSlotsFlat().ToList().Count; i++)
            {
                var stack = _inventoryComponent.AllSlotsFlat().ToList()[i];
                if (stack == null)
                    break;


                if (stack.itemName == item.itemComponent.itemPrefab.name)
                {
                    if (stack.IsFull)
                    {
                        NotflicationManager.Instance.Send("Stack Full");
                        return true;
                    }

                    stack.AddItem(item.Components);
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
            stack.AddItem(item.Components);
            return stack;
        }
        
        private bool AddStackToInventory(ItemStack stack)
        {
            int stacksCount = _inventoryComponent.HotBarAndStorage().Count(s => s != null);

            if (stacksCount >= _inventoryComponent.inventorySize)
            {
                NotflicationManager.Instance.Send("Inventory Full");
                return false;
            }

            for (int i = 0; i < _inventoryComponent.HotBarAndStorage().ToList().Count; i++)
            {
                if (_inventoryComponent.HotBarAndStorage().ToList()[i] == null)
                {
                    SlotRef slotRef = _inventoryComponent.GetSlotRef(i);
                    slotRef.List.Set(slotRef.Index, stack);
                    return true;
                }
            }

            _inventoryComponent.storage.Add(stack);
            return true;
        }
        private void HandleActiveItem(Item item)
        {
            if (_inventoryComponent.ActiveItem == null)
            {
                item.SelectItem(_owner);
                _inventoryComponent.ActiveItem = item;
                item.itemComponent.currentOwner = _owner;
                Object.DontDestroyOnLoad(item);
            }
            else
            {
                Object.Destroy(item.gameObject);
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
                int index = _inventoryComponent.AllSlotsFlat().ToList().FindIndex(itemStack => itemStack.itemName == item.itemComponent.itemPrefab.name);
                var stack = _inventoryComponent.AllSlotsFlat().ToList()[index];

                stack.RemoveItem(item.Components);

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
                stack.RemoveItem(_inventoryComponent.ActiveItem.Components);
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
                stack.RemoveItem(_inventoryComponent.ActiveItem.Components);
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
                GameObject inst = Object.Instantiate(((ItemComponent)_inventoryComponent.hotBar[index].items[0][typeof(ItemComponent)]).itemPrefab);
                var item = inst.GetComponent<Item>();
                Object.DontDestroyOnLoad(inst);
                item.InitAfterSpawnFromInventory(_inventoryComponent.hotBar[index].items[0]);
                _inventoryComponent.hotBar[index].items[0] = item.Components;
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
            Debug.Break();
            SetActiveWeapon(-1);
            _inventoryComponent.OnActiveItemChange -= OnActiveItemChange;
        }
    }

    [System.Serializable]
    public class InventoryComponent: IComponent
    {
        public float itemCheckRadius = 2f;
        public LayerMask itemLayer;
        public int CurrentActiveIndex => ActiveItem != null
            ? hotBar.Raw.FindIndex(stack =>
                stack != null && stack.itemName == _activeItem.itemComponent.itemPrefab.name)
            : -1;
        public IEnumerable<ItemStack> AllSlotsFlat() =>
            hotBar.Raw.Concat(storage.Raw).Concat(armor.Raw).Concat(accessories.Raw);
        
        public IEnumerable<ItemStack> HotBarAndStorage() =>
            hotBar.Raw.Concat(storage.Raw);
        
        [HideInInspector] public ObservableList<ItemStack> hotBar = new ObservableList<ItemStack>(5, null);
        [HideInInspector] public ObservableList<ItemStack> storage = new ObservableList<ItemStack>();
        [HideInInspector] public ObservableList<ItemStack> armor = new ObservableList<ItemStack>(5, null);
        [HideInInspector] public ObservableList<ItemStack> accessories = new ObservableList<ItemStack>(3, null);
        
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
        
        public int inventorySize = 1;
        
        
        public bool RemoveItemAnywhere(ItemStack item)
        {
            return hotBar.RemoveAndSetDefault(item)
                   || storage.RemoveAndSetDefault(item)
                   || armor.RemoveAndSetDefault(item)
                   || accessories.RemoveAndSetDefault(item);
        }
        
        
        public SlotRef GetSlotRef(int flatIndex)
        {
            if (flatIndex < hotBar.Count)
                return new SlotRef(hotBar, flatIndex);

            flatIndex -= hotBar.Count;

            if (flatIndex < storage.Count)
                return new SlotRef(storage, flatIndex);

            flatIndex -= storage.Count;

            if (flatIndex < armor.Count)
                return new SlotRef(armor, flatIndex);

            flatIndex -= armor.Count;

            return new SlotRef(accessories, flatIndex);
        }
        
        public void Swap(int firstFlatIndex, int secondFlatIndex)
        {
            if (firstFlatIndex == secondFlatIndex)
                return;

            var first = GetSlotRef(firstFlatIndex);
            var second = GetSlotRef(secondFlatIndex);

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
            set => List[Index] = value;
        }
    }
    
    [System.Serializable]
    public class ItemStack:IDisposable
    {
        public string itemName;
        [HideInInspector] public InventoryComponent inventoryComponent;

        public List<Dictionary<Type, IComponent>> items = new List<Dictionary<Type, IComponent>>();
        


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