using System.Collections.Generic;
using Controllers;
using Systems;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class PrefabViewUI : MonoBehaviour
{
    [Tooltip("Image, который показывает спрайт игрока в UI")]
    [SerializeField] private Image targetImage;

    [Tooltip("Куда спаунить. Если пусто, берётся RectTransform targetImage")]
    [SerializeField] private RectTransform container;
    [SerializeField] private string sortingLayerName = "UI";
    private PrefabViewSystem _view;
    private ColorPositioningComponent _colorComp;

    private readonly List<(PrefabViewSystem.Entry entry, GameObject go)> _items = new();
    private HashSet<uint> _targets;
    private Dictionary<uint, Vector2Int> _pixels;

    private Sprite _lastSprite;
    private Rect _lastRect;
    private bool _dirty;
    private bool _layoutDirty;

    void OnEnable()
    {
        EventBus.OnPlayerChange += Rebind;
        Rebind(ContextManager.Instance.player);
    }

    void OnDisable()
    {
        if (ContextManager.Instance != null) EventBus.OnPlayerChange -= Rebind;
        Unbind();
    }

    void Rebind(PlayerController p) => Bind(p);

    void Bind(AbstractEntity entity)
    {
        Unbind();
        if (entity == null) return;

        _view      = entity.GetControllerSystem<PrefabViewSystem>();
        _colorComp = entity.GetControllerComponent<ColorPositioningComponent>();
        if (_view == null || _colorComp == null) return;

        _targets = new HashSet<uint>();
        foreach (var g in _colorComp.pointsGroup.Values)
        {
            if (g.points == null) continue;
            foreach (var p in g.points)
                _targets.Add(SpriteColorLocator.Pack(p.color));
        }

        _view.Changed += MarkDirty;
        _dirty = true;
    }

    void Unbind()
    {
        if (_view != null) _view.Changed -= MarkDirty;
        _view = null;
        _colorComp = null;
        _targets = null;
        _pixels = null;
        _lastSprite = null;
        Clear();
    }

    void MarkDirty() => _dirty = true;

    void Clear()
    {
        foreach (var it in _items)
            if (it.go != null) Destroy(it.go);
        _items.Clear();
    }

    void Rebuild()
    {
        _dirty = false;
        _layoutDirty = true;
        Clear();
        if (_view == null || targetImage == null) return;

        var parent = container != null ? container : targetImage.rectTransform;

        foreach (var e in _view.GetEntries())
        {
            if (e.prefab == null) continue;

            var go = Instantiate(e.prefab, parent, false);
            ApplySorting(go);
            go.SetActive(false);
            _items.Add((e, go));
        }
    }
    void ApplySorting(GameObject go)
    {
        int layerId = SortingLayer.NameToID(sortingLayerName);
        if (layerId == 0 && sortingLayerName != "Default")
        {
            Debug.LogWarning($"Sorting Layer '{sortingLayerName}' не найден");
            return;
        }

        var groups = go.GetComponentsInChildren<SortingGroup>(true);
        if (groups.Length > 0)
        {
            foreach (var g in groups) g.sortingLayerID = layerId;
        }
        else
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                r.sortingLayerID = layerId;
        }
    }

    void LateUpdate()
    {
        if (_dirty) Rebuild();
        if (_items.Count == 0 || _targets == null) return;
        if (targetImage == null || targetImage.sprite == null) return;

        var sprite = targetImage.sprite;
        Rect rect = targetImage.rectTransform.rect;
        
        if (sprite == _lastSprite && rect == _lastRect && !_layoutDirty) return;

        _lastSprite = sprite;
        _lastRect = rect;
        _layoutDirty = false;

        _pixels = SpriteColorLocator.Get(sprite, _targets);
        Layout(sprite);
    }

    void Layout(Sprite sprite)
    {
        Vector2 spriteSize = sprite.rect.size;
        Rect draw = GetDrawRect(targetImage, spriteSize);
        float k = draw.width / (spriteSize.x / sprite.pixelsPerUnit);

        var imageRt = targetImage.rectTransform;
        var parent = container != null ? container : imageRt;

        foreach (var (e, go) in _items)
        {
            if (go == null) continue;

            bool found = TryGetPoints(e.pos, out var firstPx, out var lastPx);
            if (go.activeSelf != found) go.SetActive(found);
            if (!found) continue;

            Vector2 firstImage = PixelToImageLocal(firstPx, spriteSize, draw);
            Vector3 firstParent = parent == imageRt
                ? (Vector3)firstImage
                : parent.InverseTransformPoint(imageRt.TransformPoint(firstImage));

            var t = go.transform;
            var s = e.prefab.transform.localScale;
            t.localScale = new Vector3(s.x * k, s.y * k, s.z);
            t.localPosition = firstParent + (Vector3)(e.offset * k);

            float angle = 0f;
            if (e.rotate)
            {
                Vector2 dirPx = lastPx - firstPx;
                if (dirPx != Vector2.zero)
                {
                    Vector3 dirWorld = imageRt.TransformVector(new Vector3(dirPx.x, dirPx.y, 0f));
                    Vector3 dirParent = parent.InverseTransformVector(dirWorld);
                    angle = Mathf.Atan2(dirParent.y, dirParent.x) * Mathf.Rad2Deg;
                }
            }
            t.localRotation = Quaternion.Euler(0f, 0f, angle + e.rotationOffset);
        }
    }

    bool TryGetPoints(ColorPosNameConst pos, out Vector2 firstPx, out Vector2 lastPx)
    {
        firstPx = lastPx = default;
        if (_pixels == null) return false;
        if (!_colorComp.pointsGroup.TryGetValue(pos, out var group) || group.points == null)
            return false;

        bool any = false;
        foreach (var point in group.points)
        {
            if (!_pixels.TryGetValue(SpriteColorLocator.Pack(point.color), out var px)) continue;

            var p = new Vector2(px.x + 0.5f, px.y + 0.5f);
            if (!any) { firstPx = p; any = true; }
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
        Rect r = img.rectTransform.rect;
        if (!img.preserveAspect) return r;

        float s = Mathf.Min(r.width / spriteSize.x, r.height / spriteSize.y);
        Vector2 size = spriteSize * s;
        return new Rect(r.position + Vector2.Scale(r.size - size, img.rectTransform.pivot), size);
    }
}