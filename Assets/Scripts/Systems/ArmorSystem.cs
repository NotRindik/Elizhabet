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
    public class OverlayMaterial
    {
        public Material material;
        public Sprite baseSprite;
        public List<Sprite> overlaySprites = new();

        [NonSerialized] public RenderTexture rtA;
        [NonSerialized] public RenderTexture rtB;

        public void InitRT(int width, int height)
        {
            rtA = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            { filterMode = FilterMode.Point };
            rtB = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            { filterMode = FilterMode.Point };
        }

        public void ReleaseRT() { rtA?.Release(); rtB?.Release(); }
    }




    [Serializable]
    public class TextureOverlayComponent : IComponent
    {
        public SerializedDictionary<ArmourPart, OverlayMaterial[]> overlayMaterials = new();
    }

    public class TextureOverlaySystem : BaseSystem, IDisposable
    {
        private TextureOverlayComponent _overlayComponent;
        private Material _blendMaterial;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _overlayComponent = owner.GetControllerComponent<TextureOverlayComponent>();
            _blendMaterial = new Material(Shader.Find("Custom/LayerBlend"));

            foreach (var overlays in _overlayComponent.overlayMaterials.Values)
                foreach (var overlay in overlays)
                {
                    overlay.InitRT(128, 128);
                    RebuildComposite(overlay); // сразу строим с baseSprite из инспектора
                }
        }

        // добавить слой всем материалам части тела (например надеть броню)
        public void AddLayer(ArmourPart part, Sprite sprite)
        {
            foreach (var overlay in _overlayComponent.overlayMaterials[part])
            {
                overlay.overlaySprites.Add(sprite);
                RebuildComposite(overlay);
            }
        }

        public void RemoveLayer(ArmourPart part, Sprite sprite)
        {
            foreach (var overlay in _overlayComponent.overlayMaterials[part])
            {
                overlay.overlaySprites.Remove(sprite);
                RebuildComposite(overlay);
            }
        }


        public void SetBase(ArmourPart part, int materialIndex, Sprite sprite)
        {
            var overlay = _overlayComponent.overlayMaterials[part][materialIndex];
            overlay.baseSprite = sprite;
            RebuildComposite(overlay);
        }

        private void RebuildComposite(OverlayMaterial overlay)
        {
            if (overlay.baseSprite == null) return;

            var current = overlay.rtA;
            var scratch = overlay.rtB;

            Graphics.Blit(overlay.baseSprite.texture, current);

            foreach (var sprite in overlay.overlaySprites)
            {
                if (sprite == null) continue;
                _blendMaterial.SetTexture("_NewLayer", sprite.texture);
                Graphics.Blit(current, scratch, _blendMaterial);
                (current, scratch) = (scratch, current);
            }

            overlay.material.SetTexture($"_LUT{(int)LutSlotPurpose.Armour}", current);
        }

        public void Dispose()
        {
            foreach (var overlays in _overlayComponent.overlayMaterials.Values)
                foreach (var overlay in overlays)
                    overlay.ReleaseRT();

            UnityEngine.Object.Destroy(_blendMaterial);
        }
    }
}
