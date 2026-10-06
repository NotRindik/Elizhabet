using System.Collections.Generic;
using Controllers;
using Systems;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

[System.Serializable]
public class RendererImage
{
    public string rendererName;
    public Image image;
}

public class PrefabViewUI : MonoBehaviour
{
    [Tooltip("Имя GameObject рендерера игрока -> Image, который показывает его спрайт в UI")]
    [SerializeField] private RendererImage[] bindings;

    [Tooltip("Куда спаунить. Если пусто, берётся RectTransform соответствующего Image")]
    [SerializeField] private RectTransform container;
    [SerializeField] private string sortingLayerName = "UI";

    private PrefabViewSystem _view;
    private ColorPositioningComponent _colorComp;
    private readonly List<(PrefabViewSystem.Entry entry, GameObject go, Image img)> _items = new();
    private HashSet<uint> _targets;
    private bool _dirty;

    void OnEnable()
    {
        EventBus.OnPlayerChange += Rebind;
        Rebind(ContextManager.Instance.player);
    }

    void OnDisable()
    {
        if(ContextManager.Instance != null) EventBus.OnPlayerChange -= Rebind;
        Unbind();
    }

    void Rebind(PlayerController p) => Bind(p);

    void Bind(AbstractEntity entity)
    {
        Unbind();
        if(entity == null) return;

        _view = entity.GetControllerSystem<PrefabViewSystem>();
        _colorComp = entity.GetControllerComponent<ColorPositioningComponent>();
        if(_view == null || _colorComp == null) return;

        SpriteColorLocator.Clear();
        _targets = new HashSet<uint>();
        foreach(var g in _colorComp.pointsGroup.Values)
        {
            if(g.points == null) continue;
            foreach(var p in g.points)
                _targets.Add(SpriteColorLocator.Pack(p.color));
        }

        _view.Changed += MarkDirty;
        _dirty = true;
    }

    void Unbind()
    {
        if(_view != null) _view.Changed -= MarkDirty;
        _view = null;
        _colorComp = null;
        _targets = null;
        Clear();
    }

    void MarkDirty() => _dirty = true;

    void Clear()
    {
        foreach(var it in _items)
            if(it.go != null) Destroy(it.go);
        _items.Clear();
    }

    Image ResolveImage(ColorPosNameConst pos)
    {
        if(!_colorComp.pointsGroup.TryGetValue(pos, out var group)) return null;
        var sr = group.searchingRenderer != null ? group.searchingRenderer : _colorComp.spriteRenderer;
        if(sr == null) return null;
        foreach(var b in bindings)
            if(b.rendererName == sr.name) return b.image;
        return null;
    }

    void Rebuild()
    {
        _dirty = false;
        Clear();
        if(_view == null) return;

        foreach(var e in _view.GetEntries())
        {
            if(e.prefab == null) continue;
            var img = ResolveImage(e.pos);
            if(img == null) continue;

            var parent = container != null ? container : img.rectTransform;
            var go = Instantiate(e.prefab, parent, false);
            ConvertToUI(go);
            go.SetActive(false);
            _items.Add((e, go, img));
        }
    }

    void ApplySorting(GameObject go)
    {
        var layerId = SortingLayer.NameToID(sortingLayerName);
        if(layerId == 0 && sortingLayerName != "Default")
        {
            Debug.LogWarning($"Sorting Layer '{sortingLayerName}' не найден");
            return;
        }

        var groups = go.GetComponentsInChildren<SortingGroup>(true);
        if(groups.Length > 0)
        {
            foreach(var g in groups) g.sortingLayerID = layerId;
        }
        else
        {
            foreach(var r in go.GetComponentsInChildren<Renderer>(true))
                r.sortingLayerID = layerId;
        }
    }

    void LateUpdate()
    {
        if(_dirty) Rebuild();
        if(_items.Count == 0 || _targets == null) return;
        foreach(var (e, go, img) in _items)
            Layout(e, go, img);
    }

    static void ConvertToUI(GameObject root)
    {
        foreach(var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            var go = sr.gameObject;
            var t = go.transform;
            var pos = t.localPosition;
            var rot = t.localRotation;
            var scl = t.localScale;

            var img = go.AddComponent<Image>();
            img.raycastTarget = false;

            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.localPosition = pos;
            rt.localRotation = rot;
            rt.localScale = scl;

            go.AddComponent<SpriteImageSync>().Init(sr, img);
        }
    }

    void Layout(PrefabViewSystem.Entry e, GameObject go, Image img)
    {
        if(go == null) return;

        var sprite = img.sprite;
        Vector2 firstPx = default, lastPx = default;
        var found = sprite != null && TryGetPoints(sprite, e.pos, out firstPx, out lastPx);
        if(go.activeSelf != found) go.SetActive(found);
        if(!found) return;

        var spriteSize = sprite.rect.size;
        var draw = GetDrawRect(img, spriteSize);
        var k = draw.width / (spriteSize.x / sprite.pixelsPerUnit);
        var imageRt = img.rectTransform;
        var parent = container != null ? container : imageRt;

        var firstImage = PixelToImageLocal(firstPx, spriteSize, draw);
        var firstParent = parent == imageRt
            ? (Vector3)firstImage
            : parent.InverseTransformPoint(imageRt.TransformPoint(firstImage));

        var t = go.transform;
        var s = e.prefab.transform.localScale;
        t.localScale = new Vector3(s.x * k, s.y * k, s.z);
        t.localPosition = firstParent + (Vector3)(e.offset * k);

        var angle = 0f;
        if(e.rotate && lastPx != firstPx)
        {
            var lastImage = PixelToImageLocal(lastPx, spriteSize, draw);
            var dirWorld = imageRt.TransformVector(lastImage - firstImage);
            var dirParent = parent.InverseTransformVector(dirWorld);
            angle = Mathf.Atan2(dirParent.y, dirParent.x) * Mathf.Rad2Deg;
        }
        t.localRotation = Quaternion.Euler(0f, 0f, angle + e.rotationOffset);
    }

    bool TryGetPoints(Sprite sprite, ColorPosNameConst pos, out Vector2 firstPx, out Vector2 lastPx)
    {
        firstPx = lastPx = default;
        var pixels = SpriteColorLocator.Get(sprite, _targets);
        if(!_colorComp.pointsGroup.TryGetValue(pos, out var group) || group.points == null) return false;

        var any = false;
        foreach(var point in group.points)
        {
            if(!pixels.TryGetValue(SpriteColorLocator.Pack(point.color), out var px)) continue;

            var p = new Vector2(px.x + 0.5f, px.y + 0.5f);
            if(!any)
            {
                firstPx = p;
                any = true;
            }
            lastPx = p;
        }
        return any;
    }

    static Vector2 PixelToImageLocal(Vector2 px, Vector2 spriteSize, Rect draw)
    {
        return new Vector2(
            draw.xMin + px.x / spriteSize.x * draw.width,
            draw.yMin + px.y / spriteSize.y * draw.height);
    }

    static Rect GetDrawRect(Image img, Vector2 spriteSize)
    {
        var r = img.rectTransform.rect;
        if(!img.preserveAspect) return r;

        var s = Mathf.Min(r.width / spriteSize.x, r.height / spriteSize.y);
        var size = spriteSize * s;
        return new Rect(r.position + Vector2.Scale(r.size - size, img.rectTransform.pivot), size);
    }
}