using System;
using System.Collections.Generic;
using System.Linq;
using Controllers;
using UnityEngine;
using AYellowpaper.SerializedCollections;
using Assets.Scripts;
using TMPro;

namespace Systems
{
    public class InventorySlotsSystem : BaseSystem, IDisposable
    {
        private InventorySlotsComponent _inventorySlotsComponent;
        private InventoryComponent _inventoryComponent;
        private InventoryViewComponent _inventoryViewComponent;
        private StorageGrid _storageGrid;

        private readonly Dictionary<ItemStack, DragableItem> _hotbarVisuals = new();

        public AbstractEntity player;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);

            _inventorySlotsComponent = owner.GetControllerComponent<InventorySlotsComponent>();
            _inventoryComponent = owner.GetControllerComponent<InventoryComponent>();
            _inventoryViewComponent = owner.GetControllerComponent<InventoryViewComponent>();


            _inventorySlotsComponent.slots = _inventorySlotsComponent.slotsContainers
                .SelectMany(c => c.Value.GetComponentsInChildren<SlotBase>())
                .Select((slot, index) => new { index, slot })
                .ToDictionary(x => x.index, x => x.slot);

            
            var nonStorageSlots = _inventorySlotsComponent.slotsContainers
                .Where(c => c.Key != "Storage")
                .SelectMany(c => c.Value.GetComponentsInChildren<SlotBase>())
                .ToArray();

            _inventorySlotsComponent.hotSlots = nonStorageSlots.OfType<HotSlots>().ToArray();
            _inventorySlotsComponent.armourSlots = nonStorageSlots.OfType<ArmourSlot>().ToArray();

            int armorOffset = _inventoryComponent.hotBar.Count;
            int accessoriesOffset = armorOffset + _inventoryComponent.armor.Count;

            for (int i = 0; i < _inventorySlotsComponent.hotSlots.Length; i++)
            {
                var slot = _inventorySlotsComponent.hotSlots[i];
                slot.Init((i, owner));
                _inventorySlotsComponent.slots[slot.Index] = slot;
            }

            for (int i = 0; i < _inventorySlotsComponent.armourSlots.Length; i++)
            {
                var slot = _inventorySlotsComponent.armourSlots[i];
                slot.Init((armorOffset + i, owner));
                _inventorySlotsComponent.slots[slot.Index] = slot;
            }

            // если есть отдельный массив accessories-слотов — добавь сюда тем же паттерном
            // с offset = accessoriesOffset

            _storageGrid = _inventorySlotsComponent.slotsContainers["Storage"].GetComponent<StorageGrid>();
            _storageGrid.InitializeGrid(owner, _inventorySlotsComponent, _inventoryComponent, _inventoryViewComponent);

            _inventoryComponent.hotBar.OnItemChanged += OnHotBarChanged;
            
            player = ContextManager.Instance.player;
            player.GetComponent<PlayerSaveLoadManager>().IsPlayerLoadReady += Refresh;
        }

        public void Dispose()
        {
            _inventoryComponent.hotBar.OnItemChanged -= OnHotBarChanged;
            _storageGrid.DisposeGrid();
            
            player.GetComponent<PlayerSaveLoadManager>().IsPlayerLoadReady -= Refresh;
        }

        // ===== HOTBAR — без изменений по сути =====

        private void SpawnHotBarInitial()
        {
            var hotBar = _inventoryComponent.hotBar;
            var slots = _inventorySlotsComponent.hotSlots;

            for (int i = 0; i < slots.Length && i < hotBar.Count; i++)
            {
                var stack = hotBar[i];
                if (stack != null)
                    SpawnHotbarVisual(slots[i], stack, i);
            }
        }

        private void OnHotBarChanged(ItemStack _)
        {
            if (!IsActive) return;

            var hotBar = _inventoryComponent.hotBar;
            var slots = _inventorySlotsComponent.hotSlots;

            for (int i = 0; i < slots.Length && i < hotBar.Count; i++)
            {
                var stack = hotBar[i];
                var slot = slots[i];
                var current = slot.GetItem();

                if (stack == null)
                {
                    if (current != null)
                    {
                        _hotbarVisuals.Remove(current.itemData.Item);
                        slot.DestroyVisual();
                    }
                    continue;
                }

                if (current != null && ReferenceEquals(current.itemData.Item, stack))
                    continue;

                SpawnHotbarVisual(slot, stack, i);
            }
        }
        
        public void Refresh()
        {
            SpawnHotBarInitial();
            _storageGrid.Rebuild();
        }

        private void SpawnHotbarVisual(SlotBase slot, ItemStack stack, int index)
        {
            if (_hotbarVisuals.TryGetValue(stack, out var existing) && existing != null)
            {
                Debug.LogWarning($"Hotbar: {stack.itemName} уже отрисован, но список ставит его в слот {index}.");
                return;
            }

            var data = new InventoryItemData(stack, 0, index);
            slot.SetData(data);

            var spawned = slot.GetItem();
            if (spawned != null)
                _hotbarVisuals[stack] = spawned;
        }

        // ===== STORAGE — целиком делегировано StorageGrid =====

        public void SetFilter(IInventoryFilter filter)
        {
            _inventoryViewComponent.SetFilter(filter);
            _inventorySlotsComponent.storageSlotsPage.text = _inventoryViewComponent.page.ToString();
            _storageGrid.Rebuild();
        }

        public void SetPage(int i)
        {
            _inventoryViewComponent.page = Mathf.Max(i, 0);
            _inventorySlotsComponent.storageSlotsPage.text = _inventoryViewComponent.page.ToString();
            _storageGrid.Rebuild();
        }

        public void NextPage()
        {
            _inventoryViewComponent.page++;
            _inventorySlotsComponent.storageSlotsPage.text = _inventoryViewComponent.page.ToString();
            _storageGrid.Rebuild();
        }

        public void PrevPage()
        {
            _inventoryViewComponent.page = Mathf.Max(_inventoryViewComponent.page - 1, 0);
            _inventorySlotsComponent.storageSlotsPage.text = _inventoryViewComponent.page.ToString();
            _storageGrid.Rebuild();
        }

        public bool FilterAllows(InventoryItemData invItemData) => _inventoryViewComponent.FilterAllows(invItemData);
    }

    [System.Serializable]
    public class InventorySlotsComponent : IComponent
    {
        public SerializedDictionary<string, GameObject> slotsContainers;
        public DragableItem itemPrefab;
        public Dictionary<int,SlotBase> slots = new Dictionary<int,SlotBase>(); // было SlotBase[]
        public StorageSlot[] storageSlots;      // заполняет сам StorageGrid
        public ArmourSlot[] armourSlots;
        public HotSlots[] hotSlots;
        public TextMeshProUGUI storageSlotsPage;
    }

    [Serializable]
    public class InventoryViewComponent : IComponent
    {
        private IInventoryFilter _filter;
        public int page = 0;
        public int storageCount;

        public void SetFilter(IInventoryFilter filter)
        {
            _filter = filter;
            page = 0;
        }

        public bool FilterAllows(InventoryItemData item)
        {
            if (item == null) return false;
            return _filter == null || _filter.Filter(item);
        }
    }

    public interface IInventoryFilter
    {
        bool Filter(InventoryItemData item);
        public enum FilterType { None, Weapons, MeleeWeapons, Foods, Armours }
    }

    public static class InventoryFilters
    {
        public static readonly Dictionary<IInventoryFilter.FilterType, IInventoryFilter> Filters = new()
        {
            { IInventoryFilter.FilterType.None, null },
            { IInventoryFilter.FilterType.Weapons, new FilterByWeapon() },
            { IInventoryFilter.FilterType.Armours, new FilterByArmor() }
        };
    }

    public class FilterByArmor : IInventoryFilter
    {
        public bool Filter(InventoryItemData item) => item.Item.GetItemComponent<ArmourItemComponent>() != null;
    }

    public class FilterByWeapon : IInventoryFilter
    {
        public bool Filter(InventoryItemData item) => item.Item.GetItemComponent<WeaponComponent>() != null;
    }
}