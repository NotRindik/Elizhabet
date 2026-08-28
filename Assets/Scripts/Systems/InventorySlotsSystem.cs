using System;
using System.Collections.Generic;
using System.Linq;
using Controllers;
using UnityEngine;
using AYellowpaper.SerializedCollections;
using TMPro;

namespace Systems
{
    public class InventorySlotsSystem : BaseSystem, IDisposable
    {
        private InventorySlotsComponent _inventorySlotsComponent;
        private InventoryComponent _inventoryComponent => owner.GetControllerComponent<InventoryComponent>();
        private InventoryViewComponent _inventoryViewComponent;
        private StorageGrid _storageGrid;

        private readonly Dictionary<ItemStack, DragableItem> _hotbarVisuals = new();

        public AbstractEntity player;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);

            _inventorySlotsComponent = owner.GetControllerComponent<InventorySlotsComponent>();
            _inventoryViewComponent = owner.GetControllerComponent<InventoryViewComponent>();

            _inventorySlotsComponent.AllSlots = _inventorySlotsComponent.slotsContainers
                .SelectMany(c => c.Value.GetComponentsInChildren<SlotBase>())
                .ToArray();


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
            _inventorySlotsComponent.modSlots = nonStorageSlots.OfType<ModSlot>().ToArray();

            int armorOffset = _inventoryComponent.hotBar.Count;
            int accessoriesOffset = armorOffset + _inventoryComponent.armor.Count;

            for (int i = 0; i < _inventorySlotsComponent.hotSlots.Length; i++)
            {
                var slot = _inventorySlotsComponent.hotSlots[i];
                slot.Init((i, owner));
                _inventorySlotsComponent.slots[slot.Index] = slot;
            }
            
            for (int i = 0; i < _inventorySlotsComponent.modSlots.Length; i++)
            {
                var slot = _inventorySlotsComponent.modSlots[i];
                slot.Init((accessoriesOffset + i, owner));
                _inventorySlotsComponent.slots[slot.Index] = slot;
            }

            for (int i = 0; i < _inventorySlotsComponent.armourSlots.Length; i++)
            {
                var slot = _inventorySlotsComponent.armourSlots[i];
                slot.Init((armorOffset + i, owner));
                _inventorySlotsComponent.slots[slot.Index] = slot;
            }
            
            _storageGrid = _inventorySlotsComponent.slotsContainers["Storage"].GetComponent<StorageGrid>();
            _storageGrid.InitializeGrid(owner, _inventorySlotsComponent, _inventoryComponent, _inventoryViewComponent);

            _inventoryComponent.hotBar.OnItemChanged += OnHotBarChanged;
        }
        public void ReInitPlayer()
        {
            player = ContextManager.Instance.player;
            player.GetComponent<PlayerSaveLoadManager>().IsPlayerLoadReady += ReInit;
        }

        public void ReInit()
        {
            ClearAllVisualElements();
            Refresh();
        }
        public void ClearAllVisualElements()
        {
            foreach (var slot in _inventorySlotsComponent.AllSlots)
            {
                slot.DestroyVisual();
            }
            _hotbarVisuals.Clear();
        }

        public void Dispose()
        {
            _inventoryComponent.hotBar.OnItemChanged -= OnHotBarChanged;
            _storageGrid.DisposeGrid();
            
            player.GetComponent<PlayerSaveLoadManager>().IsPlayerLoadReady -= ReInit;
        }

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
        public Dictionary<int,SlotBase> slots = new Dictionary<int,SlotBase>();
        public StorageSlot[] storageSlots;
        public ArmourSlot[] armourSlots;
        public ModSlot[] modSlots;
        public HotSlots[] hotSlots;
        public TextMeshProUGUI storageSlotsPage, storageCapacityText;

        public SlotBase[] AllSlots;
    }

    [Serializable]
    public class InventoryViewComponent : IComponent
    {
        public IInventoryFilter Filter { get; private set; }
        public int page = 0;
        public int storageCount;

        public void SetFilter(IInventoryFilter filter)
        {
            Filter = filter;
            page = 0;
        }

        public bool FilterAllows(InventoryItemData item)
        {
            if (item == null) return false;
            return Filter == null || Filter.Filter(item);
        }
    }

    public interface IInventoryFilter
    {
        bool Filter(InventoryItemData item);
    }
    
    public enum ItemCategory { None, Weapons, Foods, Armours, Modificators, Resources }

    public static class InventoryFilters
    {
        public static readonly Dictionary<ItemCategory, IInventoryFilter> Filters = new()
        {
            { ItemCategory.None, null },
            { ItemCategory.Weapons, new FilterByWeapon() },
            { ItemCategory.Armours, new FilterByArmor() }
        };
        
        public static readonly Dictionary<Type, ItemCategory> FilterTypes = new()
        {
            { typeof(FilterByWeapon), ItemCategory.Weapons },
            { typeof(FilterByArmor),ItemCategory.Armours}
        };
    }

    public class FilterByArmor : IInventoryFilter
    {
        public bool Filter(InventoryItemData item) => item.Item.GetItemComponentFromConfig<ArmourItemComponent>() != null;
    }

    public class FilterByWeapon : IInventoryFilter
    {
        public bool Filter(InventoryItemData item) => item.Item.GetItemComponentFromConfig<WeaponComponent>() != null;
    }
}