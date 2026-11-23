#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using Controllers;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Systems
{
    [ExecuteAlways]
    public class ColorPositioningPreview : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;

        [Tooltip("Сеттеры, которые двигаются в редакторе. Порядок вызова = PositionSetter.priority.")]
        [SerializeField] private List<PositionSetter> editorSetters = new();

        [Space]
        [LabelText("Work / Unwork")]
        [Tooltip("Самостоятельная работа без Multi Animator. Позиции откатываются при выключении.")]
        [OnValueChanged(nameof(OnWorkToggled))]
        [SerializeField] private bool work;

        private readonly Dictionary<Texture2D, Color32[]> _pixels = new();
        private readonly Dictionary<Sprite, Dictionary<int, Vector2Int>> _spritePoints = new();
        private readonly List<PositionSetter> _ordered = new();
        private readonly HashSet<Transform> _driven = new();
        private readonly Dictionary<Transform, Vector3> _restore = new();
        private readonly HashSet<Texture2D> _warned = new();
        private bool _orderDirty = true;

        public ColorPositioningComponent Component
        {
            get
            {
                if (playerController == null)
                    playerController = GetComponentInParent<PlayerController>(true);
                return playerController != null ? playerController.colorPositioningComponent : null;
            }
        }

        public bool IsAvailable => Component != null && Setters.Count > 0;

        public IReadOnlyList<PositionSetter> Setters
        {
            get
            {
                RebuildOrder();
                return _ordered;
            }
        }

        public bool Drives(Transform target)
        {
            RebuildOrder();
            return target != null && _driven.Contains(target);
        }

        public bool Apply()
        {
            var component = Component;
            if (component == null || component.pointsGroup == null)
                return false;

            RebuildOrder();
            foreach (var setter in _ordered)
            {
                if (setter == null || !component.pointsGroup.TryGetValue(setter.nameConst, out var group))
                    continue;

                var renderer = group.searchingRenderer ? group.searchingRenderer : component.spriteRenderer;
                if (TryGetGroupPosition(group, renderer, out var world))
                    setter.ApplyEditorPosition(world);
            }
            return true;
        }

        public bool TryGetGroupPoints(ColorPosNameConst key, List<Vector2> localPoints, out SpriteRenderer renderer)
        {
            localPoints.Clear();
            renderer = null;

            var component = Component;
            if (component == null || component.pointsGroup == null ||
                !component.pointsGroup.TryGetValue(key, out var group) || group.points == null)
                return false;

            renderer = group.searchingRenderer ? group.searchingRenderer : component.spriteRenderer;
            if (renderer == null || renderer.sprite == null)
                return false;

            var map = PointsOf(renderer.sprite);
            if (map == null)
                return false;

            foreach (var point in group.points)
                if (map.TryGetValue(Pack(point.color), out var px))
                    localPoints.Add(PixelToLocal(px.x, px.y, renderer));

            return localPoints.Count > 0;
        }

        [Button("Собрать сеттеры")]
        public void CollectSetters()
        {
            var root = playerController != null ? playerController.transform : transform;
#if UNITY_EDITOR
            Undo.RecordObject(this, "Collect Setters");
#endif
            editorSetters = new List<PositionSetter>(root.GetComponentsInChildren<PositionSetter>(true));
            _orderDirty = true;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        [Button("Сбросить кэш пикселей")]
        public void ClearCache()
        {
            _pixels.Clear();
            _spritePoints.Clear();
            _warned.Clear();
        }

        private void OnValidate() => _orderDirty = true;

        private void OnEnable()
        {
            ClearCache();
            _orderDirty = true;
#if UNITY_EDITOR
            if (work && !Application.isPlaying)
                BeginStandalone();
            EditorApplication.update += EditorTick;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif
            EndStandalone();
        }

        private void OnWorkToggled()
        {
            if (work)
                BeginStandalone();
            else
                EndStandalone();
        }

        private void BeginStandalone()
        {
            _restore.Clear();
            foreach (var setter in Setters)
                if (setter != null)
                    _restore[setter.transform] = setter.transform.localPosition;
        }

        private void EndStandalone()
        {
            foreach (var (t, position) in _restore)
                if (t != null)
                    t.localPosition = position;
            _restore.Clear();
        }

#if UNITY_EDITOR
        private void EditorTick()
        {
            if (!work || Application.isPlaying || AnimationMode.InAnimationMode())
                return;
            Apply();
        }
#endif

        private void RebuildOrder()
        {
            if (!_orderDirty && _ordered.Count > 0)
                return;

            _orderDirty = false;
            _ordered.Clear();
            _driven.Clear();

            var indexed = new List<(PositionSetter setter, int index)>();
            for (int i = 0; i < editorSetters.Count; i++)
                if (editorSetters[i] != null)
                    indexed.Add((editorSetters[i], i));

            indexed.Sort((a, b) =>
            {
                int c = a.setter.priority.CompareTo(b.setter.priority);
                return c != 0 ? c : a.index.CompareTo(b.index);
            });

            foreach (var (setter, _) in indexed)
            {
                _ordered.Add(setter);
                _driven.Add(setter.transform);
            }
        }

        private bool TryGetGroupPosition(ColorPointGroup group, SpriteRenderer renderer, out Vector3 world)
        {
            world = default;
            if (renderer == null || renderer.sprite == null || group.points == null)
                return false;

            var map = PointsOf(renderer.sprite);
            if (map == null)
                return false;

            foreach (var point in group.points)
            {
                if (!map.TryGetValue(Pack(point.color), out var px))
                    continue;
                world = PixelToWorld(px.x, px.y, renderer);
                return true;
            }
            return false;
        }

        private Dictionary<int, Vector2Int> PointsOf(Sprite sprite)
        {
            if (_spritePoints.TryGetValue(sprite, out var map))
                return map;

            var tex = sprite.texture;
            if (!tex.isReadable)
            {
                if (_warned.Add(tex))
                    Debug.LogWarning($"[{nameof(ColorPositioningPreview)}] Текстура '{tex.name}' не Read/Write, включи Read/Write в Import Settings.", tex);
                _spritePoints[sprite] = null;
                return null;
            }

            if (!_pixels.TryGetValue(tex, out var pixels))
                _pixels[tex] = pixels = tex.GetPixels32();

            map = new Dictionary<int, Vector2Int>();
            var rect = sprite.textureRect;
            int rx = (int)rect.x, ry = (int)rect.y, rw = (int)rect.width, rh = (int)rect.height;
            int width = tex.width;

            for (int y = ry; y < ry + rh; y++)
            {
                int row = y * width;
                for (int x = rx; x < rx + rw; x++)
                {
                    var p = pixels[row + x];
                    if (p.a == 0)
                        continue;
                    map.TryAdd(Pack(p), new Vector2Int(x, y));
                }
            }

            _spritePoints[sprite] = map;
            return map;
        }

        private static int Pack(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

        private static Vector3 PixelToWorld(int x, int y, SpriteRenderer sr) =>
            sr.transform.TransformPoint(PixelToLocal(x, y, sr));

        private static Vector2 PixelToLocal(int x, int y, SpriteRenderer sr)
        {
            var sprite = sr.sprite;
            float ppu = sprite.pixelsPerUnit;
            Vector2 rectSize = sprite.rect.size;
            Vector2 pivot = sprite.pivot;

            var local = new Vector2((x + 0.5f - pivot.x) / ppu, (y + 0.5f - pivot.y) / ppu);

            if (sr.drawMode != SpriteDrawMode.Simple)
            {
                Vector2 spriteSize = rectSize / ppu;
                if (spriteSize.x != 0f) local.x *= sr.size.x / spriteSize.x;
                if (spriteSize.y != 0f) local.y *= sr.size.y / spriteSize.y;
            }

            return local;
        }
    }
}
