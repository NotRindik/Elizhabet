using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace Systems
{
public class ArmorSystem : BaseSystem, IDisposable
{
    private InventoryComponent _inventoryComponent;
    private ProtectionComponent _protectionComponent;
    private TextureOverlaySystem _textureOverlay;
    
    private PrefabViewSystem _prefabView;
    private readonly Dictionary<ArmourPart, ItemStack> _lastVisibleStack = new();

    private readonly ItemStack[] _lastKnown = new ItemStack[6];

    private PlayerSaveLoadManager _saveLoad;

    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);

        _inventoryComponent = owner.GetControllerComponent<InventoryComponent>();
        _protectionComponent = owner.GetControllerComponent<ProtectionComponent>();
        _textureOverlay = owner.GetControllerSystem<TextureOverlaySystem>();
        _prefabView = owner.GetControllerSystem<PrefabViewSystem>();

        _inventoryComponent.armor.OnItemChanged += OnArmorChanged;

        _saveLoad = owner.GetComponent<PlayerSaveLoadManager>();
        if (_saveLoad != null)
            _saveLoad.IsPlayerLoadReady += Resync;

        Resync();
    }

    public void Dispose()
    {
        _inventoryComponent.armor.OnItemChanged -= OnArmorChanged;
        if (_saveLoad != null)
            _saveLoad.IsPlayerLoadReady -= Resync;
    }

    private void OnArmorChanged(ItemStack _) => Resync();

    private readonly Dictionary<ArmourPart, Sprite> _lastVisibleSprite = new();

    private void Resync()
    {
        var raw = _inventoryComponent.armor.Raw;

        foreach (ArmourPart part in Enum.GetValues(typeof(ArmourPart)))
        {
            int armourIdx   = ArmourSlotIndex.ToFlatIndex(ArmourType.Armour,   part);
            int cosmeticIdx = ArmourSlotIndex.ToFlatIndex(ArmourType.Cosmetic, part);

            var armourStack   = armourIdx   < raw.Count ? raw[armourIdx]   : null;
            var cosmeticStack = cosmeticIdx < raw.Count ? raw[cosmeticIdx] : null;

            var visibleStack = cosmeticStack ?? armourStack;
            var armourComp   = visibleStack?.GetItemComponent<ArmourItemComponent>();
            var newSprite    = armourComp?.armourSprite;

            _lastVisibleSprite.TryGetValue(part, out var prevSprite);

            if (!ReferenceEquals(prevSprite, newSprite))
            {
                if (prevSprite != null) _textureOverlay.RemoveLayer(part, prevSprite);
                if (newSprite  != null) _textureOverlay.AddLayer(part, newSprite);
                _lastVisibleSprite[part] = newSprite;
            }
            
            _lastVisibleStack.TryGetValue(part, out var prevStack);
            if (!ReferenceEquals(prevStack, visibleStack))
            {
                if (prevStack != null) _prefabView.Hide(prevStack);

                var data = armourComp?.prefabViewData;
                if (visibleStack != null && data != null && data.Length > 0)
                    _prefabView.Show(visibleStack, data);

                _lastVisibleStack[part] = visibleStack;
            }

            ApplyDiff(armourIdx,   armourStack,   isProtective: true);
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
        public ArmourPart[] coveredParts;
        public Sprite baseSprite;
        public List<Sprite> overlaySprites = new();

        [NonSerialized] public RenderTexture rtA;
        [NonSerialized] public RenderTexture rtB;
        
        [NonSerialized] public Material instance;

        public void InitRT(int width, int height)
        {
            instance = new Material(material);
            rtA = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            rtB = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
        }

        public void ReleaseRT()
        {
            if (rtA != null) { rtA.Release(); UnityEngine.Object.Destroy(rtA); }
            if (rtB != null) { rtB.Release(); UnityEngine.Object.Destroy(rtB); }
            if (instance != null) UnityEngine.Object.Destroy(instance);
        }

        public bool Covers(ArmourPart part) => Array.IndexOf(coveredParts, part) != -1;
    }

    [Serializable]
    public class TextureOverlayComponent : IComponent
    {
        public OverlayMaterial[] overlayMaterials;
    }

    public class TextureOverlaySystem : BaseSystem, IDisposable
    {
        private TextureOverlayComponent _overlayComponent;
        private Material _blendMaterial;
        
        public event Action InstancesChanged;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _overlayComponent = owner.GetControllerComponent<TextureOverlayComponent>();
            _blendMaterial = new Material(Shader.Find("Custom/LayerBlend"));

            foreach (var overlay in _overlayComponent.overlayMaterials)
            {
                overlay.InitRT(128, 86);
                RebuildComposite(overlay);
            }

            InstancesChanged?.Invoke();
        }
        
        public bool TryGetInstance(Material template, out Material instance)
        {
            instance = null;
            if (_overlayComponent == null) return false;

            foreach (var o in _overlayComponent.overlayMaterials)
            {
                if (o.material == template)
                {
                    instance = o.instance;
                    return instance != null;
                }
            }
            return false;
        }

        public void AddLayer(ArmourPart part, Sprite sprite)
        {
            foreach (var overlay in _overlayComponent.overlayMaterials)
            {
                if (!overlay.Covers(part)) continue;
                overlay.overlaySprites.Add(sprite);
                RebuildComposite(overlay);
            }
        }

        public void RemoveLayer(ArmourPart part, Sprite sprite)
        {
            foreach (var overlay in _overlayComponent.overlayMaterials)
            {
                if (!overlay.Covers(part)) continue;
                overlay.overlaySprites.Remove(sprite);
                RebuildComposite(overlay);
            }
        }

        public void SetBase(ArmourPart part, Sprite sprite)
        {
            foreach (var overlay in _overlayComponent.overlayMaterials)
            {
                if (!overlay.Covers(part)) continue;
                overlay.baseSprite = sprite;
                RebuildComposite(overlay);
            }
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

            // ВАЖНО: пишем в instance, а не в общий material
            overlay.instance.SetTexture($"_LUT{(int)LutSlotPurpose.Armour}", current);
        }

        public void Dispose()
        {
            InstancesChanged = null;

            foreach (var overlay in _overlayComponent.overlayMaterials)
                overlay.ReleaseRT();

            UnityEngine.Object.Destroy(_blendMaterial);
        }
    }
}
namespace Systems
{
    [Serializable]
    public class PrefabViewComponent : IComponent
    {
        [Tooltip("Куда класть инстансы. Если пусто, берётся transform владельца")]
        public Transform container;
    }

    public class PrefabViewSystem : BaseSystem, IDisposable
    {
        private class Entry
        {
            public GameObject go;
            public ColorPosNameConst pos;
            public Vector2 offset;
            public bool rotate;
            public float rotationOffset;
        }

        private PrefabViewComponent _component;
        private ColorPositioningComponent _colorComponent;
        private Transform _container;
        
        private readonly Dictionary<object, List<Entry>> _views = new();

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);

            _component = owner.GetControllerComponent<PrefabViewComponent>();
            _colorComponent = owner.GetControllerComponent<ColorPositioningComponent>();
            _container = _component.container != null ? _component.container : owner.transform;

            _colorComponent.AfterColorCalculated.Add(UpdatePositions, 0);
        }

        public void Show(object key, ArmourItemComponent.PrefabViewData[] data)
        {
            if (key == null || data == null || data.Length == 0) return;

            Hide(key);

            var list = new List<Entry>(data.Length);
            foreach (var d in data)
            {
                if (d?.prefab == null) continue;

                var go = UnityEngine.Object.Instantiate(d.prefab, _container);
                go.SetActive(false);

                list.Add(new Entry
                {
                    go = go,
                    pos = d.nameConst,
                    offset = d.offset,
                    rotate = d.rotateByDirection,
                    rotationOffset = d.rotationOffset 
                });
            }

            if (list.Count > 0) _views[key] = list;
        }

        public void Hide(object key)
        {
            if (key == null || !_views.TryGetValue(key, out var list)) return;

            foreach (var e in list)
                if (e.go != null) UnityEngine.Object.Destroy(e.go);

            _views.Remove(key);
        }

        private void UpdatePositions()
        {
            foreach (var list in _views.Values)
            {
                foreach (var e in list)
                {
                    if (e.go == null) continue;

                    bool found = false;

                    if (_colorComponent.pointsGroup.TryGetValue(e.pos, out var group))
                    {
                        var p = group.FirstActivePoint();
                        if (p != Vector2.zero)
                        {
                            var t = e.go.transform;
                            t.position = new Vector3(p.x + e.offset.x, p.y + e.offset.y, t.position.z);

                            float baseAngle = 0f;
                            if (e.rotate)
                            {
                                var dir = group.direction;
                                if (dir != Vector2.zero)
                                    baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                            }
                            t.rotation = Quaternion.Euler(0, 0, baseAngle + e.rotationOffset);
                            found = true;
                        }
                    }

                    if (e.go.activeSelf != found) e.go.SetActive(found);
                }
            }
        }

        public void Dispose()
        {
            _colorComponent?.AfterColorCalculated.Remove(UpdatePositions);

            foreach (var list in _views.Values)
                foreach (var e in list)
                    if (e.go != null) UnityEngine.Object.Destroy(e.go);

            _views.Clear();
        }
    }
}