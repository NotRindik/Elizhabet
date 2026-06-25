using AYellowpaper.SerializedCollections;
using Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Systems
{
public class ArmorSystem : BaseSystem, IDisposable
{
    private InventoryComponent _inventoryComponent;
    private ProtectionComponent _protectionComponent;
    private TextureOverlaySystem _textureOverlay;

    private readonly ItemStack[] _lastKnown = new ItemStack[6];

    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);

        _inventoryComponent = owner.GetControllerComponent<InventoryComponent>();
        _protectionComponent = owner.GetControllerComponent<ProtectionComponent>();
        _textureOverlay = owner.GetControllerSystem<TextureOverlaySystem>();

        _inventoryComponent.armor.OnItemChanged += OnArmorChanged;
        Resync();
    }

    public void Dispose() => _inventoryComponent.armor.OnItemChanged -= OnArmorChanged;

    private void OnArmorChanged(ItemStack _) => Resync();

    private void Resync()
    {
        var raw = _inventoryComponent.armor.Raw;

        foreach (ArmourPart part in Enum.GetValues(typeof(ArmourPart)))
        {
            int armourIdx = ArmourSlotIndex.ToFlatIndex(ArmourType.Armour, part);
            int cosmeticIdx = ArmourSlotIndex.ToFlatIndex(ArmourType.Cosmetic, part);

            var armourStack = armourIdx < raw.Count ? raw[armourIdx] : null;
            var cosmeticStack = cosmeticIdx < raw.Count ? raw[cosmeticIdx] : null;
            
            var visibleStack = cosmeticStack ?? armourStack;
            var texture = visibleStack?.GetItemComponent<ArmourItemComponent>()?.armourSprite?.texture;
            _textureOverlay.SetStaticSlot(part, LutSlotPurpose.Armour, texture);

            ApplyDiff(armourIdx, armourStack, isProtective: true);
            ApplyDiff(cosmeticIdx, cosmeticStack, isProtective: false);
        }
    }

    private void ApplyDiff(int index, ItemStack current, bool isProtective)
    {
        var previous = _lastKnown[index];
        if (ReferenceEquals(previous, current)) return;

        if (previous != null)
        {
            var prevArmour = previous.GetItemComponent<ArmourItemComponent>();
            if (prevArmour != null) prevArmour.isEquiped = false;
            if (isProtective && prevArmour != null) _protectionComponent.RemoveModifire(prevArmour);
        }

        if (current != null)
        {
            var currArmour = current.GetItemComponent<ArmourItemComponent>();
            if (currArmour != null) currArmour.isEquiped = true;
            if (isProtective && currArmour != null) _protectionComponent.AddModifire(currArmour);
        }

        _lastKnown[index] = current;
    }
}

    public enum ArmourPart
    {
        Head,
        Torso,
        Leg
    }

    public enum ArmourType
    {
        Cosmetic,
        Armour
    }

    public static class ArmourSlotIndex
    {
        // порядок ArmourPart: Head=0, Torso=1, Leg=2
        public static int ToFlatIndex(ArmourType type, ArmourPart part) =>
            (type == ArmourType.Armour ? 0 : 3) + (int)part;
    }
    
    
    [System.Serializable]
    public class ProtectionComponent : IComponent
    {
        [SerializeField]private float _baseProtection;
        [SerializeField]  private List<ArmourItemComponent> _modifiers = new List<ArmourItemComponent>();

        public Action<float> OnProtectionChange;
        public float Protection
        {
            get
            {
                var protection = _baseProtection + _modifiers.Sum(a => a.protection);
                return protection;
            }
        }


        public void AddModifire(ArmourItemComponent _modifire)
        {
            _modifiers.Add(_modifire);
            OnProtectionChange?.Invoke(Protection);
        }

        public void RemoveModifire(ArmourItemComponent _modifire)
        {
            _modifiers.Remove(_modifire);
            OnProtectionChange?.Invoke(Protection);
        }
    }
    
    public enum LutSlotPurpose
    {
        PlayerBase = 1,
        Armour = 2,
        VisualReserved1 = 3,
        VisualReserved2 = 4
    }
    
    [Serializable]
    public class TextureOverlayComponent : IComponent
    {
        public SerializedDictionary<ArmourPart, Material[]> armourMaterial = new();
    }
    
    public class TextureOverlaySystem : BaseSystem, IDisposable
    {
        private const int StaticSlotCount = 4;
        private const int TotalSlotCount = 10;

        private TextureOverlayComponent _overlayComponent;
        
        private readonly Dictionary<ArmourPart, bool[]> _dynamicSlotUsed = new();
        private readonly Dictionary<ArmourPart, Dictionary<object, int>> _dynamicSlotOwners = new();

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _overlayComponent = owner.GetControllerComponent<TextureOverlayComponent>();

            foreach (ArmourPart part in Enum.GetValues(typeof(ArmourPart)))
            {
                _dynamicSlotUsed[part] = new bool[TotalSlotCount - StaticSlotCount];
                _dynamicSlotOwners[part] = new Dictionary<object, int>();
            }
        }

        public void SetStaticSlot(ArmourPart part, LutSlotPurpose slot, Texture texture)
        {
            foreach (var renderer in _overlayComponent.armourMaterial[part])
                renderer.SetTexture($"_LUT{(int)slot}", texture);
        }
        
        public bool TryApplyEffect(ArmourPart part, object effectKey, Texture texture)
        {
            var owners = _dynamicSlotOwners[part];
            var used = _dynamicSlotUsed[part];

            if (owners.TryGetValue(effectKey, out int existingSlot))
            {
                ApplyToMaterial(part, StaticSlotCount + existingSlot + 1, texture);
                return true;
            }

            for (int i = 0; i < used.Length; i++)
            {
                if (used[i]) continue;

                used[i] = true;
                owners[effectKey] = i;
                ApplyToMaterial(part, StaticSlotCount + i + 1, texture);
                return true;
            }

            Debug.LogWarning($"Все динамические LUT-слоты на {part} заняты — эффект {effectKey} не применён");
            return false;
        }

        public void RemoveEffect(ArmourPart part, object effectKey)
        {
            var owners = _dynamicSlotOwners[part];
            if (!owners.TryGetValue(effectKey, out int slot)) return;

            _dynamicSlotUsed[part][slot] = false;
            owners.Remove(effectKey);
            ApplyToMaterial(part, StaticSlotCount + slot + 1, null);
        }

        private void ApplyToMaterial(ArmourPart part, int lutIndex, Texture texture)
        {
            foreach (var renderer in _overlayComponent.armourMaterial[part])
                renderer.SetTexture($"_LUT{lutIndex}", texture);
        }

        public void Dispose()
        {
            foreach (var part in _dynamicSlotOwners.Keys.ToList())
                foreach (var key in _dynamicSlotOwners[part].Keys.ToList())
                    RemoveEffect(part, key);
        }
    }
}
