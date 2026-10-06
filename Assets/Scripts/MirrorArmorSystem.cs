using System;
using System.Collections.Generic;
using Controllers;
using Systems;
using UnityEngine;

[Serializable]
public class MirrorArmorSystem : BaseSystem, IDisposable
{
    private TextureOverlaySystem _overlay;
    private PrefabViewSystem _prefabView;
    private InventoryComponent _inv;
    private PlayerSaveLoadManager _saveLoad;
    private readonly Dictionary<ArmourPart, Sprite> _sprites = new();
    private readonly Dictionary<ArmourPart, ItemStack> _stacks = new();

    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        _overlay = owner.GetControllerSystem<TextureOverlaySystem>();
        _prefabView = owner.GetControllerSystem<PrefabViewSystem>();

        EventBus.OnPlayerChange += Bind;
        if(ContextManager.Instance != null) Bind(ContextManager.Instance.player);
    }

    void Bind(PlayerController p)
    {
        Unbind();
        if(p == null) return;

        _inv = p.GetControllerComponent<InventoryComponent>();
        if(_inv == null) return;

        _inv.armor.OnItemChanged += OnChanged;
        _saveLoad = p.GetComponent<PlayerSaveLoadManager>();
        if(_saveLoad != null) _saveLoad.IsPlayerLoadReady += Resync;
        Resync();
    }

    void Unbind()
    {
        if(_inv != null) _inv.armor.OnItemChanged -= OnChanged;
        if(_saveLoad != null) _saveLoad.IsPlayerLoadReady -= Resync;
        _inv = null;
        _saveLoad = null;

        foreach(var kv in _sprites)
            if(kv.Value != null) _overlay.RemoveLayer(kv.Key, kv.Value);
        foreach(var kv in _stacks)
            if(kv.Value != null) _prefabView.Hide(kv.Value);
        _sprites.Clear();
        _stacks.Clear();
    }

    void OnChanged(ItemStack _) => Resync();
    
    public int PreviewSortID = -1;

    void Resync()
    {
        if(_inv == null) return;
        var raw = _inv.armor.Raw;

        if (PreviewSortID == -1)
        {
            PreviewSortID = SortingLayer.NameToID("Preview");
        }
        
        foreach(ArmourPart part in Enum.GetValues(typeof(ArmourPart)))
        {
            var armourIdx = ArmourSlotIndex.ToFlatIndex(ArmourType.Armour, part);
            var cosmeticIdx = ArmourSlotIndex.ToFlatIndex(ArmourType.Cosmetic, part);
            var armourStack = armourIdx < raw.Count ? raw[armourIdx] : null;
            var cosmeticStack = cosmeticIdx < raw.Count ? raw[cosmeticIdx] : null;

            var visible = cosmeticStack ?? armourStack;
            var comp = visible?.GetItemComponent<ArmourItemComponent>();
            var newSprite = comp?.armourSprite;

            _sprites.TryGetValue(part, out var prevSprite);
            if(!ReferenceEquals(prevSprite, newSprite))
            {
                if(prevSprite != null) _overlay.RemoveLayer(part, prevSprite);
                if(newSprite != null) _overlay.AddLayer(part, newSprite);
                _sprites[part] = newSprite;
            }

            _stacks.TryGetValue(part, out var prevStack);
            if(!ReferenceEquals(prevStack, visible))
            {
                if(prevStack != null) _prefabView.Hide(prevStack);
                var data = comp?.prefabViewData;
                if(data != null)
                    foreach (var d in data)
                        d.SortingLayer = PreviewSortID;
                if(visible != null && data != null && data.Length > 0) _prefabView.Show(visible, data);
                _stacks[part] = visible;
            }
        }
    }

    public void Dispose()
    {
        EventBus.OnPlayerChange -= Bind;
        Unbind();
    }
}