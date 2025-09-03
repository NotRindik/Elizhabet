using System;
using System.Collections.Generic;
using System.Linq;
using Controllers;
using UnityEngine;
using AYellowpaper.SerializedCollections;
using Assets.Scripts;
using TMPro;
using System.Threading.Tasks;

namespace Systems
{
    public class InventorySlotsSystem : BaseSystem,IDisposable
    {
        private InventorySlotsComponent _inventorySlotsComponent;
        private InventoryComponent _inventoryComponent;

        private InventoryViewComponent _inventoryViewComponent;
        private InventorySystem _inventorySystem;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);

            _inventorySlotsComponent = owner.GetControllerComponent<InventorySlotsComponent>();
            _inventorySystem = owner.GetControllerSystem<InventorySystem>();
            _inventoryComponent = base.owner.GetControllerComponent<InventoryComponent>();
            _inventoryViewComponent = owner.GetControllerComponent<InventoryViewComponent>();

            _inventorySlotsComponent.slots = _inventorySlotsComponent.slotsContainers.SelectMany(container => container.Value.GetComponentsInChildren<SlotBase>()).ToArray();
            _inventorySlotsComponent.conveyorSlots = _inventorySlotsComponent.slots.Where((slot, index) => index <= 4 && slot is СonveyorSlot).Cast<СonveyorSlot>().ToArray();
            _inventorySlotsComponent.armourSlots = _inventorySlotsComponent.slots.Where((slot) => slot is ArmourSlot).Cast<ArmourSlot>().ToArray();
            _inventorySlotsComponent.storageSlots = _inventorySlotsComponent.slots.Where((slot) => slot is StorageSlot).Cast<StorageSlot>().ToArray();

            for (int i = 0; i < _inventorySlotsComponent.slots.Length; i++)
            {
                _inventorySlotsComponent.slots[i].Init((i, owner));
            }

            _inventoryViewComponent.storageCount = _inventorySlotsComponent.storageSlots.Length;
            _inventorySlotsComponent.storageSlotsPage.text = _inventoryViewComponent.page.ToString();

            _inventoryComponent.items.OnItemChanged += UpdateViewModel;

            _inventoryViewComponent.onViewDataChanged += UpdateDisplayedData;

        }

        private void UpdateDisplayedData(List<InventoryItemData> items)
        {
            var slots = _inventorySlotsComponent.slots;

            HashSet<InventoryItemData> currentItems = new HashSet<InventoryItemData>();
            foreach (var slot in slots)
            {
                var existing = slot.GetItem();
                if (existing != null)
                    currentItems.Add(existing.itemData);
            }


            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];

                if (currentItems.Contains(item) || item == null)
                    continue;

                if (i < 5)
                {
                    TryPlaceIn(_inventorySlotsComponent.conveyorSlots, item);
                }
                else
                {
                    TryPlaceIn(_inventorySlotsComponent.storageSlots, item);
                }
            }

            HashSet<InventoryItemData> validItems = new HashSet<InventoryItemData>(items);
            foreach (var slot in slots)
            {
                var itemVisual = slot.GetItem();
                if (itemVisual != null && !validItems.Contains(itemVisual.itemData))
                {
                    slot.DestroyVisual();
                }
            }
        }

        private void TryPlaceIn<TSlot>(TSlot[] slots, InventoryItemData item) where TSlot : SlotBase
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty && item.SlotIndex == -1)
                {
                    slots[i].SetData(item);
                    break;
                }
                else if (slots[i].Index == item.SlotIndex)
                {
                    slots[i].SetData(item);
                }
            }
        }
        private void UpdateViewModel(ItemStack _)
        {
            if(!IsActive)
                return;

            _inventoryViewComponent.UpdateItems(_inventoryComponent.items.Raw);
        }

        public void Dispose()
        {
            _inventoryComponent.items.OnItemChanged -= UpdateViewModel;
        }

        public void OnPageChange()
        {
            foreach (var item in _inventorySlotsComponent.storageSlots)
            {
                item.currPage = _inventoryViewComponent.page;
            }
            _inventorySlotsComponent.storageSlotsPage.text = _inventoryViewComponent.page.ToString();

            _inventoryViewComponent.UpdateItems(_inventoryComponent.items.Raw);
        }

        public void SetFilter(IInventoryFilter filter)
        {
            foreach (var item in _inventorySlotsComponent.storageSlots)
            {
                item.DestroyVisual();
            }
            _inventoryViewComponent.SetFilter(filter);

            foreach (var item in _inventorySlotsComponent.storageSlots)
            {
                item.currPage = _inventoryViewComponent.page;
            }
            _inventorySlotsComponent.storageSlotsPage.text = _inventoryViewComponent.page.ToString();

            _inventoryViewComponent.UpdateItems(_inventoryComponent.items.Raw);
        }

        public void SetPage(int i)
        {
            _inventoryViewComponent.page = Mathf.Max(i, 0);
            OnPageChange();
        }

        public void NextPage()
        {
            _inventoryViewComponent.page++;
            OnPageChange();
        }
        public void PrevPage()
        {
            _inventoryViewComponent.page = Mathf.Max(_inventoryViewComponent.page-1,0);
            OnPageChange();
        }
    }
    [System.Serializable]
    public class InventorySlotsComponent : IComponent
    {
        public SerializedDictionary<string,GameObject> slotsContainers;

        public DragableItem itemPrefab;
        public SlotBase[] slots;
        public StorageSlot[] storageSlots;
        public ArmourSlot[] armourSlots;
        public СonveyorSlot[] conveyorSlots;

        public TextMeshProUGUI storageSlotsPage;
    }

    [Serializable]
    public class InventoryViewComponent : IComponent
    {
        public ObservableList<InventoryItemData> DisplayedItems = new ObservableList<InventoryItemData>();
        public List<InventoryItemData> ViewModel = new List<InventoryItemData>();
        public TextMeshProUGUI slotsSortingText;

        public delegate void OnViewDataChanged(List<InventoryItemData> DisplayedItems);
        public event OnViewDataChanged onViewDataChanged;

        private IInventoryFilter _filter;

        public int storageCount;
        public int page = 0;

        public void SetFilter(IInventoryFilter filter)
        {
            var storagedItems = ViewModel.Skip(5)
            .Where(item => item != null)
            .Where(item =>
            {
                var armour = item.Item.GetItemComponent<ArmourItemComponent>();
                return armour == null || !armour.isEquiped;
            })
            .ToList();

            foreach (var item in storagedItems)
            {
                item.SlotIndex = -1;
                item.PageIndex = 0;
            }
            _filter = filter;
            page = 0;
        }

        public async void UpdateItems(List<ItemStack> source)
        {

            SyncViewModel(source);

            var filteredItems = await ApplyFilter(ViewModel);

            slotsSortingText.text = "";

            var paged = filteredItems
                .Where(item => item != null && item.PageIndex == page) // <-- только текущая страница
                .Where(item =>
                {
                    var armour = item.Item.GetItemComponent<ArmourItemComponent>();
                    return armour == null || !armour.isEquiped;
                })
                .Take(storageCount)
                .ToList();

            AssignFrom(paged);

            for (int i = 0; i < 5; i++)
            {
                var targetItem = source[i];
                DisplayedItems.Insert(i, ViewModel[i]);
            }

            DisplayedItems.Raw.AddRange(ViewModel.FindAll(item => { 
                if(item == null)
                    return false;
                var armour = item.Item.GetItemComponent<ArmourItemComponent>();

                if(armour == null) return false;

                return armour.isEquiped;
            }));
            Debug.Log("Before VieData Change");
            onViewDataChanged?.Invoke(DisplayedItems.Raw);
            Debug.Log("After VieData Change");
        }
        public void SyncViewModel(List<ItemStack> source)
        {
            // Кэшируем существующие данные для O(1) поиска
            var lookup = new Dictionary<ItemStack, InventoryItemData>(ViewModel.Count);
            foreach (var vm in ViewModel)
            {
                if (vm != null && vm.Item != null)
                    lookup[vm.Item] = vm;
            }

            var newList = new List<InventoryItemData>(source.Count);

            foreach (var stack in source)
            {
                if (stack == null)
                {
                    newList.Add(null); // пустая ячейка
                    continue;
                }

                if (lookup.TryGetValue(stack, out var existing))
                {
                    newList.Add(existing);
                }
                else
                {
                    newList.Add(new InventoryItemData(stack, page, -1));
                }
            }

            ViewModel.Clear();
            ViewModel.AddRange(newList);
        }


        public void AssignFrom(List<InventoryItemData> other)
        {
            // Удаляем элементы, которых нет в "other"
            for (int i = DisplayedItems.Count - 1; i >= 0; i--)
            {
                if (!other.Contains(DisplayedItems[i]))
                    DisplayedItems.Raw.RemoveAt(i);
            }

            // Добавляем недостающие ссылки
            foreach (var item in other)
            {
                if (item != null && !DisplayedItems.Raw.Contains(item))
                    DisplayedItems.Raw.Add(item);
            }
        }


        public bool FilterAllows(InventoryItemData item)
        {
            if(item == null)
                return false;
            return _filter == null || _filter.Filter(item);
        }

        private async Task<List<InventoryItemData>> ApplyFilter(List<InventoryItemData> source)
        {
            List<InventoryItemData> result = new List<InventoryItemData>();
            const int batchSize = 100;
            int counter = 0;

            for (int i = 5; i < source.Count; i++)
            {
                InventoryItemData item = source[i];

                if (FilterAllows(item))
                {
                    result.Add(item);
                }

                counter++;
                if (counter >= batchSize)
                {
                    counter = 0;
                    await Task.Yield();
                }
            }

            return result;
        }


    }


    public interface IInventoryFilter
    {
        bool Filter(InventoryItemData item);

        public enum FilterType 
        {
            None,Weapons,MeleeWeapons,Foods,Armours
        }
    }


    public static class InventoryFilters
    {
        public static readonly Dictionary<IInventoryFilter.FilterType, IInventoryFilter> Filters
            = new()
        {
        { IInventoryFilter.FilterType.None, null },
        { IInventoryFilter.FilterType.Weapons, new FilterByWeapon() },
        { IInventoryFilter.FilterType.Armours, new FilterByArmor() }
        };
    }

    public class FilterByArmor : IInventoryFilter
    {
        public bool Filter(InventoryItemData item)
        {
            return item.Item.GetItemComponent<ArmourItemComponent>() != null;
        }
    }

    public class FilterByWeapon : IInventoryFilter
    {
        public bool Filter(InventoryItemData item)
        {
            return item.Item.GetItemComponent<WeaponComponent>() != null;
        }
    }
}