using System;
using System.Collections.Generic;
using System.Linq;
using Controllers;
using UnityEngine;
using AYellowpaper.SerializedCollections;
using std;
using TMPro;

namespace Systems
{
    public class InventorySlotsSystem : BaseSystem, IDisposable
    {
        private InventorySlotsComponent _inventorySlotsComponent;
        private InventoryComponent _inventoryComponent => ContextManager.Instance.player.GetControllerComponent<InventoryComponent>();
        private InventoryViewComponent _inventoryViewComponent;
        private StorageGrid _storageGrid;

        private readonly Dictionary<ItemStack, DragableItem> _slotVisuals = new();

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

            _inventoryComponent.hotBar.OnItemChanged += OnSlotListChanged;
            _inventoryComponent.armor.OnItemChanged += OnSlotListChanged;
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
            _slotVisuals.Clear();
        }

        public void Dispose()
        {
            _inventoryComponent.hotBar.OnItemChanged -= OnSlotListChanged;
            _inventoryComponent.armor.OnItemChanged -= OnSlotListChanged;
            _storageGrid.DisposeGrid();
            
            player.GetComponent<PlayerSaveLoadManager>().IsPlayerLoadReady -= ReInit;
        }
        private void OnSlotListChanged(ItemStack _)
        {
            if (!IsActive) return;
            SyncFixedSlots();
        }

        private void SpawnFixedInitial() => SyncFixedSlots();

        private void SyncFixedSlots()
        {
            var hotBar = _inventoryComponent.hotBar;
            var armor = _inventoryComponent.armor;
            
            ClearMismatched(_inventorySlotsComponent.hotSlots, hotBar);
            ClearMismatched(_inventorySlotsComponent.armourSlots, armor);
            
            SpawnMissing(_inventorySlotsComponent.hotSlots, hotBar);
            SpawnMissing(_inventorySlotsComponent.armourSlots, armor);
        }

        private void ClearMismatched(SlotBase[] slots, ObservableList<ItemStack> list)
        {
            for (int i = 0; i < slots.Length && i < list.Count; i++)
            {
                var slot = slots[i];
                var current = slot.GetItem();
                if (current == null) continue;

                var stack = list[i];
                if (stack != null && ReferenceEquals(current.itemData.Item, stack))
                    continue;

                _slotVisuals.Remove(current.itemData.Item);
                slot.DestroyVisual();
            }
        }

        private void SpawnMissing(SlotBase[] slots, ObservableList<ItemStack> list)
        {
            for (int i = 0; i < slots.Length && i < list.Count; i++)
            {
                var stack = list[i];
                if (stack == null) continue;

                var slot = slots[i];
                if (slot.GetItem() != null) continue;

                slot.SetData(new InventoryItemData(stack, 0, slot.Index));

                var spawned = slot.GetItem();
                if (spawned != null)
                    _slotVisuals[stack] = spawned;
            }
        }
        
        public void Refresh()
        {
            SpawnFixedInitial();
            _storageGrid.Rebuild();
        }
        

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