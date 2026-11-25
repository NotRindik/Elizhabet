using System;
using System.Collections.Generic;
using UnityEngine;

namespace Systems
{
    [CreateAssetMenu(menuName = "Eliza/Color Points Bake", fileName = "ColorPointsBake")]
    public class ColorPointsBake : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public Sprite sprite;
            public int[] colors;
            public Vector2Int[] pixels;
        }

        public UnityEngine.Object[] sources = Array.Empty<UnityEngine.Object>();
        public Color32[] trackedColors = Array.Empty<Color32>();
        public List<Entry> entries = new();

        private Dictionary<Sprite, Dictionary<int, Vector2Int>> _map;

        public static int Pack(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

        public bool Contains(Sprite sprite)
        {
            BuildMap();
            return sprite != null && _map.ContainsKey(sprite);
        }

        public bool TryGetPixel(Sprite sprite, Color32 color, out Vector2Int localPixel)
        {
            localPixel = default;
            if (sprite == null)
                return false;
            BuildMap();
            return _map.TryGetValue(sprite, out var points) && points.TryGetValue(Pack(color), out localPixel);
        }

        public static Vector2 LocalPixelToLocal(Vector2Int localPixel, SpriteRenderer sr)
        {
            var sprite = sr.sprite;
            float ppu = sprite.pixelsPerUnit;
            Vector2 pivot = sprite.pivot;
            var local = new Vector2((localPixel.x + 0.5f - pivot.x) / ppu, (localPixel.y + 0.5f - pivot.y) / ppu);

            if (sr.drawMode != SpriteDrawMode.Simple)
            {
                Vector2 spriteSize = sprite.rect.size / ppu;
                if (spriteSize.x != 0f) local.x *= sr.size.x / spriteSize.x;
                if (spriteSize.y != 0f) local.y *= sr.size.y / spriteSize.y;
            }

            return local;
        }

        public void Invalidate() => _map = null;

        private void OnEnable() => _map = null;

        private void BuildMap()
        {
            if (_map != null)
                return;

            _map = new Dictionary<Sprite, Dictionary<int, Vector2Int>>();
            foreach (var entry in entries)
            {
                if (entry.sprite == null || entry.colors == null || entry.pixels == null)
                    continue;

                var points = new Dictionary<int, Vector2Int>(entry.colors.Length);
                int count = Mathf.Min(entry.colors.Length, entry.pixels.Length);
                for (int i = 0; i < count; i++)
                    points[entry.colors[i]] = entry.pixels[i];
                _map[entry.sprite] = points;
            }
        }
    }
}
