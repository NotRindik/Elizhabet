using System;
using System.Collections.Generic;
using System.Linq;
using Controllers;
using UnityEngine;
using AYellowpaper.SerializedCollections;
using Assets.Scripts;
using Unity.VisualScripting;
using TMPro;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using System.Threading.Tasks;
using System.Threading;

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

        private void UpdateDisplayedData(List<ItemStack> items)
        {
            var slots = _inventorySlotsComponent.slots;

            HashSet<ItemStack> currentItems = new HashSet<ItemStack>();
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
                else if (_inventoryViewComponent.FilterAllows(item))
                {
                    TryPlaceIn(_inventorySlotsComponent.storageSlots, item);
                }
            }

            HashSet<ItemStack> validItems = new HashSet<ItemStack>(items);
            foreach (var slot in slots)
            {
                var itemData = slot.GetItem();
                if (itemData != null && !validItems.Contains(itemData.itemData))
                {
                    slot.DestroyVisual();
                }
            }
        }

        private void TryPlaceIn<TSlot>(TSlot[] slots, ItemStack item) where TSlot : SlotBase
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty)
                {
                    slots[i].SetData(item);
                    break;
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
            // 1. Очищаем ВСЕ слоты
            foreach (var slot in _inventorySlotsComponent.storageSlots)
            {
                slot.DestroyVisual();
            }

            _inventoryViewComponent.UpdateItems(_inventoryComponent.items.Raw);
            _inventorySlotsComponent.storageSlotsPage.text = _inventoryViewComponent.page.ToString();

            foreach (var item in _inventorySlotsComponent.storageSlots)
            {
                item.currPage = _inventoryViewComponent.page;
            }
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
        public ObservableList<ItemStack> DisplayedItems = new ObservableList<ItemStack>();
        public TextMeshProUGUI slotsSortingText;

        public delegate void OnViewDataChanged(List<ItemStack> DisplayedItems);
        public event OnViewDataChanged onViewDataChanged;

        private IInventoryFilter _filter;

        public int storageCount;
        public int page = 0;

        public void SetFilter(IInventoryFilter filter)
        {
            _filter = filter;
            page = 0;
        }

        public async void UpdateItems(List<ItemStack> source)
        {
            var cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;

            // Запускаем анимацию текста
            var animationTask = AnimateSortingText(token);

            var filteredItems = await ApplyFilter(source);

            cts.Cancel();
            slotsSortingText.text = "";

            var paged = filteredItems.Skip(page * storageCount).Take(storageCount).ToList();

            DisplayedItems.AssignFrom(paged);

            for (int i = 0; i < 5; i++)
            {
                var targetItem = source[i];
                DisplayedItems.Insert(i, targetItem);
            }

            onViewDataChanged?.Invoke(DisplayedItems.Raw);
        }
        public bool FilterAllows(ItemStack item)
        {
            return _filter == null || _filter.Filter(item);
        }
        private async Task AnimateSortingText(CancellationToken token)
        {
            string baseText = "Sorting";
            int dotCount = 0;

            while (!token.IsCancellationRequested)
            {
                dotCount = (dotCount + 1) % 4; // 0, 1, 2, 3
                slotsSortingText.text = baseText + new string('.', dotCount);
                await Task.Delay(500, token); // Каждые полсекунды обновляем
            }
        }
        private async Task<List<ItemStack>> ApplyFilter(List<ItemStack> source)
        {
            List<ItemStack> result = new List<ItemStack>();
            const int batchSize = 100;
            int counter = 0;

            for (int i = 5; i < source.Count; i++)
            {
                ItemStack item = source[i];

                if (_filter == null || _filter.Filter(item))
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
        bool Filter(ItemStack item);
    }

    public class FilterByArmor : IInventoryFilter
    {
        public bool Filter(ItemStack item)
        {
            return item.GetItemComponent<ArmourItemComponent>() != null;
        }
    }

    public class FilterByWeapon : IInventoryFilter
    {
        public bool Filter(ItemStack item)
        {
            return item.GetItemComponent<WeaponComponent>() != null;
        }
    }
}