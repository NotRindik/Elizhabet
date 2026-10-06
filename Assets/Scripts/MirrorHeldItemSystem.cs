using System;
using System.Collections.Generic;
using Controllers;
using Systems;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class MirrorHeldItemSystem : BaseSystem, IDisposable
{
    class Node
    {
        public Transform src;
        public Transform dst;
        public SpriteRenderer srcSr;
        public SpriteRenderer dstSr;
    }

    [SerializeField] private int sortingOrderOffset;

    private AbstractEntity _owner;
    private ColorPositioningComponent _colorPositioning;
    private InventoryComponent _inv;
    private Transform _mirrorRoot;
    private Item _item;
    private ItemPositioningSystem _positioning;
    private Action _tick;
    private readonly List<Node> _nodes = new();

    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        _owner = owner;
        _colorPositioning = owner.GetControllerComponent<ColorPositioningComponent>();
        if(_colorPositioning == null)
        {
            Debug.LogError("[MirrorHeldItemSystem] У клона нет ColorPositioningComponent");
            return;
        }

        _tick = Tick;
        _colorPositioning.AfterColorCalculated.Add(_tick, 3);

        EventBus.OnPlayerChange += Bind;
        if(ContextManager.Instance != null) Bind(ContextManager.Instance.player);
    }

    void Bind(PlayerController p)
    {
        Unbind();
        if(p == null) return;

        _inv = p.GetControllerComponent<InventoryComponent>();
        if(_inv == null) return;

        _inv.OnActiveItemChange += OnItemChanged;
        Build(_inv.ActiveItem);
    }

    void Unbind()
    {
        if(_inv != null) _inv.OnActiveItemChange -= OnItemChanged;
        _inv = null;
        Clear();
    }

    void OnItemChanged(Item current, Item previous) => Build(current);

    void Clear()
    {
        if(_mirrorRoot != null) UnityEngine.Object.Destroy(_mirrorRoot.gameObject);
        _mirrorRoot = null;
        _item = null;
        _positioning = null;
        _nodes.Clear();
    }

    void Build(Item item)
    {
        Clear();
        if(item == null) return;

        _item = item;
        var go = new GameObject("MirrorHeldItem") { layer = _owner.gameObject.layer };
        SceneManager.MoveGameObjectToScene(go, _owner.gameObject.scene);
        _mirrorRoot = go.transform;
        AddNode(item.transform, _mirrorRoot);
    }

    void AddNode(Transform src, Transform dst)
    {
        var node = new Node { src = src, dst = dst };
        node.srcSr = src.GetComponent<SpriteRenderer>();
        if(node.srcSr != null) node.dstSr = dst.gameObject.AddComponent<SpriteRenderer>();
        _nodes.Add(node);

        for(var i = 0; i < src.childCount; i++)
        {
            var child = src.GetChild(i);
            var go = new GameObject(child.name) { layer = dst.gameObject.layer };
            go.transform.SetParent(dst, false);
            AddNode(child, go.transform);
        }
    }

    void UpdatePositioning()
    {
        var src = _item.itemPositioningSystem;
        if(src == null) return;
        if(_positioning != null && _positioning.GetType() == src.GetType()) return;
        _positioning = (ItemPositioningSystem)Activator.CreateInstance(src.GetType());
    }

    void Tick()
    {
        if(_mirrorRoot == null) return;
        if(_item == null)
        {
            Clear();
            return;
        }

        UpdatePositioning();
        if(_positioning != null)
            _positioning.Apply(_colorPositioning, _owner.mono.transform, _mirrorRoot);

        for(var i = 0; i < _nodes.Count; i++)
        {
            var n = _nodes[i];
            if(n.src == null) continue;

            if(i > 0)
            {
                n.dst.localPosition = n.src.localPosition;
                n.dst.localRotation = n.src.localRotation;
                n.dst.localScale = n.src.localScale;
            }

            var active = n.src.gameObject.activeSelf;
            if(n.dst.gameObject.activeSelf != active) n.dst.gameObject.SetActive(active);

            if(n.dstSr == null) continue;
            var s = n.srcSr;
            var d = n.dstSr;
            d.enabled = s.enabled;
            d.sprite = s.sprite;
            d.color = s.color;
            d.flipX = s.flipX;
            d.flipY = s.flipY;
            d.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default"));
            d.sortingLayerID = s.sortingLayerID;
            d.sortingOrder = s.sortingOrder + sortingOrderOffset;
            d.sortingLayerName = "Preview";
        }
    }

    public void Dispose()
    {
        if(_colorPositioning != null && _tick != null) _colorPositioning.AfterColorCalculated.Remove(_tick);
        EventBus.OnPlayerChange -= Bind;
        Unbind();
    }
}