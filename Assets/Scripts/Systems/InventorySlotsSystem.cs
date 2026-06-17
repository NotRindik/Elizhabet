    using System;
    using System.Linq;
    using AYellowpaper.SerializedCollections;
    using Controllers;
    using Systems;
    using TMPro;
    using UnityEngine;
    using Object = UnityEngine.Object;

    namespace Systems
    {
        public class InventorySlotsSystem : BaseSystem, IDisposable
        {
            private InventorySlotsComponent _slots;
            private InventoryComponent _inv;
            private InventoryViewComponent _view;
            private InventorySystem _inventorySystem;

            // ═══════════════════════════════════════════════════
            // INIT
            // ═══════════════════════════════════════════════════

            public override void Initialize(AbstractEntity owner)
            {
                base.Initialize(owner);

                _slots           = owner.GetControllerComponent<InventorySlotsComponent>();
                _inv             = owner.GetControllerComponent<InventoryComponent>();
                _view            = owner.GetControllerComponent<InventoryViewComponent>();
                _inventorySystem = owner.GetControllerSystem<InventorySystem>();

                _slots.allSlots   = _slots.slotsContainers
                    .SelectMany(c => c.Value.GetComponentsInChildren<SlotBase>())
                    .ToArray();
                _slots.hotBarSlots = _slots.allSlots.OfType<HotBarSlot>().ToArray();
                _slots.armourSlots = _slots.allSlots.OfType<ArmourSlot>().ToArray();

                for (int i = 0; i < _slots.allSlots.Length; i++)
                    _slots.allSlots[i].Init((i, owner));

                _slots.storageGrid.Init(owner);         // инитим грид

                _inv.hotBar.OnChanged  += RedrawHotBar;
                _inv.storage.OnChanged += RedrawStorage;
                
                _inventorySystem.OnSwapped += OnSwapped;

                RedrawHotBar();
                RedrawStorage();

                _slots.storageSlotsPage.text = _view.page.ToString();
            }
            
            
            private void OnSwapped(InventorySystem.SlotRef from, InventorySystem.SlotRef to)
            {
                var itemFrom = FindVisualBySlotRef(from);
                var itemTo   = FindVisualBySlotRef(to);

                if (itemFrom != null) itemFrom.SetSourceSlot(to);
                if (itemTo   != null) itemTo.SetSourceSlot(from);

                // hotBar → storage
                if (from.IsHotBar && !to.IsHotBar)
                {
                    if (itemFrom != null)
                        itemFrom.SetParent(_slots.storageGrid.gridSlots[to.Index].transform);
                    if (itemTo != null)
                        itemTo.SetParent(_slots.hotBarSlots[from.Index].transform);
            
                    // Обновляем ItemVisual в слоте хотбара
                    _slots.hotBarSlots[from.Index].Clear();
                }
                // storage → hotBar
                else if (!from.IsHotBar && to.IsHotBar)
                {
                    if (itemFrom != null)
                        itemFrom.SetParent(_slots.hotBarSlots[to.Index].transform);
                    if (itemTo != null)
                        itemTo.SetParent(_slots.storageGrid.transform);

                    _slots.hotBarSlots[to.Index].Clear();
                    if (itemFrom != null)
                    {
                        _slots.storageGrid.dragableItems.Remove(itemFrom);
                    }
                }
                // hotBar → hotBar
                else if (from.IsHotBar && to.IsHotBar)
                {
                    if (itemFrom != null)
                        itemFrom.SetParent(_slots.hotBarSlots[to.Index].transform);
                    if (itemTo != null )
                        itemTo.SetParent(_slots.hotBarSlots[from.Index].transform);

                    _slots.hotBarSlots[from.Index].Clear();
                    _slots.hotBarSlots[to.Index].Clear();
                }
                // storage → storage: только SourceSlot, парент тот же грид
            }
            private DragableItem FindVisualBySlotRef(InventorySystem.SlotRef slot)
            {
                if (slot.IsHotBar)
                    return _slots.hotBarSlots[slot.Index].GetItem();

                // В гриде ищем по SourceSlot
                foreach (var d in _slots.storageGrid.dragableItems)
                {
                    if (d != null && d.SourceSlot.Equals(slot))
                        return d;
                }
                return null;
            }


            // ═══════════════════════════════════════════════════
            // REDRAW
            // ═══════════════════════════════════════════════════

            private void RedrawStorage()
            {
                Debug.Log("RedrawsStorage");
                
                // Чистим старые айтемы в гриде
                foreach (GameObject child in _slots.storageGrid.gridSlots)
                {
                    for (int i = 0; i < child.transform.childCount; i++)
                    {
                        Object.Destroy(child.transform.GetChild(i).gameObject);
                    }
                }

                var filtered = _inv.storage.items
                    .Where(s => s != null && s.Count > 0)
                    .Where(s => _view.filter == null || _view.filter.Filter(s))
                    .ToList();

                int pageSize  = 20;                     // или вынеси в InventoryViewComponent
                int pageStart = _view.page * pageSize;
                var paged     = filtered.Skip(pageStart).Take(pageSize).ToList();

                _view.maxPage = filtered.Count > 0 ? (filtered.Count - 1) / pageSize : 0;

                // Спавним DragableItem прямо в грид
                for (int i = 0; i < paged.Count; i++)
                {
                    var instance = Object.Instantiate(_slots.itemPrefab, _slots.storageGrid.transform);
                    var slotRef  = new InventorySystem.SlotRef(isHotBar: false, pageStart + i);
                    instance.Init(paged[i], slotRef, _inventorySystem);

                    var itemComponent = paged[i].GetItemComponent<ItemComponent>();
                    instance.image.sprite = itemComponent?.itemIcon;
                    instance.image.color  = Color.white;
                }

                _slots.storageSlotsPage.text = _view.page.ToString();
            }
            private void RedrawHotBar()
            {
                for (int i = 0; i < _slots.hotBarSlots.Length; i++)
                {
                    var stack = i < _inv.hotBar.slots.Length ? _inv.hotBar.slots[i] : null;

                    if (stack != null && stack.Count > 0)
                        _slots.hotBarSlots[i].SetData(new InventoryItemData(stack, 0, i));
                    else
                        _slots.hotBarSlots[i].DestroyVisual();
                }
            }
            

            // ═══════════════════════════════════════════════════
            // ФИЛЬТР / ПАГИНАЦИЯ — вызывается из UI кнопок
            // ═══════════════════════════════════════════════════

            public void SetFilter(IInventoryFilter filter)
            {
                _view.filter = filter;
                _view.page   = 0;
                RedrawStorage();
            }

            public void SetPage(int page)
            {
                _view.page = Mathf.Clamp(page, 0, _view.maxPage);
                RedrawStorage();
            }

            public void NextPage()  => SetPage(_view.page + 1);
            public void PrevPage()  => SetPage(_view.page - 1);

            // ═══════════════════════════════════════════════════
            // DISPOSE
            // ═══════════════════════════════════════════════════

            public void Dispose()
            {
                _inv.hotBar.OnChanged  -= RedrawHotBar;
                _inv.storage.OnChanged -= RedrawStorage;
                _inventorySystem.OnSwapped -= OnSwapped;
            }
        }

        // ═══════════════════════════════════════════════════════
        // КОМПОНЕНТЫ
        // ═══════════════════════════════════════════════════════

        [Serializable]
        public class InventorySlotsComponent : IComponent
        {
            public SerializedDictionary<string, GameObject> slotsContainers;
            public DragableItem itemPrefab;
            public StorageGrid storageGrid;         // ссылка на GridLayoutGroup с StorageGrid

            public SlotBase[]   allSlots;
            public HotBarSlot[] hotBarSlots;
            public ArmourSlot[] armourSlots;

            public TextMeshProUGUI storageSlotsPage;
        }

        [Serializable]
        public class InventoryViewComponent : IComponent
        {
            public IInventoryFilter filter;
            public int page    = 0;
            public int maxPage = 0;          // вычисляется в RedrawStorage
            public TextMeshProUGUI slotsSortingText;
        }
    }

    public interface IInventoryFilter
    {
        bool Filter(ItemStack stack);
    }

    public class FilterByWeapon : IInventoryFilter
    {
        public bool Filter(ItemStack stack) 
            => stack.GetItemComponent<WeaponComponent>() != null;
    }

    public class FilterByArmor : IInventoryFilter
    {
        public bool Filter(ItemStack stack) 
            => stack.GetItemComponent<ArmourItemComponent>() != null;
    }

    public static class InventoryFilterFactory
    {
        private static readonly IInventoryFilter[] _filters =
        {
            null,                  // 0 — None
            new FilterByWeapon(),  // 1
            new FilterByArmor(),   // 2
            // добавляй сюда новые
        };

        // Этот метод вешаешь на UnityEvent кнопки, передаёшь int
        public static IInventoryFilter Get(int index)
        {
            if (index < 0 || index >= _filters.Length)
                return null;
            return _filters[index];
        }
    }