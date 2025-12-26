using AYellowpaper.SerializedCollections;
using Controllers;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Systems
{
    public class ColorPositioningSystem : BaseSystem, IDisposable
    {
        ColorPositioningComponent colorComponent;

        private Sprite lastSprite;

        private Dictionary<Color32, Vector2Int> cachedLocalPositions = new();

        public SpriteRenderer[] currentSpriteRenderer;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            colorComponent = owner.GetControllerComponent<ColorPositioningComponent>();

            owner.OnLateUpdate += Update;
        }
        public override void OnUpdate()
        {
            UpdateSpriteData();
            UpdateWorldPositions();
            colorComponent.AfterColorCalculated.Invoke();
        }

        private unsafe void UpdateSpriteData()
        {
            if (colorComponent == null) return;

            // Список всех SpriteRenderer, которые участвуют
            List<SpriteRenderer> renderers = new List<SpriteRenderer>();
            if (colorComponent.spriteRenderer != null)
            {
                renderers.Add(colorComponent.spriteRenderer);
            }
            else
            {
                foreach (var group in colorComponent.pointsGroup.Values)
                {
                    if (group.searchingRenderer != null)
                        renderers.Add(group.searchingRenderer);
                }
            }

            if (renderers.Count == 0) return;

            cachedLocalPositions.Clear();

            foreach (var sr in renderers)
            {
                var sprite = sr.sprite;
                if (sprite == null) continue;

                var texture = sprite.texture;
                int width = texture.width;
                int height = texture.height;

                NativeArray<Color32> rawTextureData = texture.GetRawTextureData<Color32>();
                Color32* pixelPtr = (Color32*)NativeArrayUnsafeUtility.GetUnsafePtr(rawTextureData);

                foreach (var pointGroup in colorComponent.pointsGroup)
                {
                    if (pointGroup.Value.searchingRenderer != null && pointGroup.Value.searchingRenderer != sr)
                        continue;

                    for (int z = 0; z < pointGroup.Value.points.Length; z++)
                    {
                        var point = pointGroup.Value.points[z];
                        bool found = false;

                        for (int y = 0; y < height && !found; y++)
                        {
                            for (int x = 0; x < width && !found; x++)
                            {
                                Color32 pixelColor = *(pixelPtr + (y * width + x));
                                if (pixelColor.a == 0) continue;

                                if (pixelColor.r == point.color.r &&
                                    pixelColor.g == point.color.g &&
                                    pixelColor.b == point.color.b)
                                {
                                    cachedLocalPositions[point.color] = new Vector2Int(x, y);
                                    found = true;
                                }
                            }
                        }

                        if (!found)
                        {
                            cachedLocalPositions[point.color] = new Vector2Int(-1, -1);
                        }
                    }
                }

                rawTextureData.Dispose();
            }
        }
        

        public void ForceUpdatePosition()
        {
            UpdateWorldPositions();
        }

        private void UpdateWorldPositions()
        {
            if (colorComponent == null) return;

            foreach (var pointGroup in colorComponent.pointsGroup)
            {
                var targetRenderer = pointGroup.Value.searchingRenderer ?? colorComponent.spriteRenderer;

                for (int z = 0; z < pointGroup.Value.points.Length; z++)
                {
                    ref var point = ref pointGroup.Value.points[z];

                    if (cachedLocalPositions.TryGetValue(point.color, out var px) && px.x >= 0)
                    {
                        point.position = PixelToWorldPosition(px.x, px.y, targetRenderer);
                    }
                    else
                    {
                        point.position = Vector3.zero;
                    }
                }
            }
        }

        private Vector3 PixelToWorldPosition(int x, int y, SpriteRenderer sr)
        {
            var sprite = sr.sprite;
            float ppu = sprite.pixelsPerUnit;

            // Размер вырезки спрайта в пикселях
            Vector2 rectSizePx = sprite.rect.size;
            Vector2 pivotPx = sprite.pivot;
            Rect texRect = sprite.textureRect;

            // Координаты пикселя в рамках именно спрайта (а не всей текстуры)
            float sx = (float)x;
            float sy = (float)y;

            // Центр пикселя + смещение относительно pivot
            float dxPx = sx + 0.5f - pivotPx.x;
            float dyPx = sy + 0.5f - pivotPx.y;

            // Локальные координаты в юнитах
            Vector2 local = new Vector2(dxPx / ppu, dyPx / ppu);

            // Если используется Sliced/Tiled, масштабируем вручную
            if (sr.drawMode != SpriteDrawMode.Simple)
            {
                Vector2 spriteWorldSize = rectSizePx / ppu;
                Vector2 targetSize = sr.size;
                if (spriteWorldSize.x != 0f) local.x *= targetSize.x / spriteWorldSize.x;
                if (spriteWorldSize.y != 0f) local.y *= targetSize.y / spriteWorldSize.y;
            }

            // Применяем позицию/масштаб/поворот (и твой scale -1 тоже сюда войдёт)
            return sr.transform.TransformPoint(local);
        }

        public void Dispose()
        {
            owner.OnUpdate += OnUpdate;
        }
    }

    [Serializable]
    public class ColorPositioningComponent : IComponent
    {
        public SpriteRenderer spriteRenderer;
        [SerializedDictionary] public SerializedDictionary<ColorPosNameConst, ColorPointGroup> pointsGroup = new SerializedDictionary<ColorPosNameConst, ColorPointGroup>();
        public PriorityAction AfterColorCalculated = new();
    }
    
    [Serializable]
    public struct ColorPointGroup
    {
        public ColorPoint[] points;
        public Vector2 direction => GetDirection();

        public SpriteRenderer searchingRenderer;

        private Vector2 GetDirection()
        {
            if (points.Length < 2) return Vector2.zero;

            int validCount = 0;
            Vector2 first = Vector2.zero, last = Vector2.zero;

            foreach (var point in points)
            {
                if (point.position == Vector3.zero) continue;

                if (validCount == 0)
                {
                    first = point.position;
                }
                last = point.position;
                validCount++;
            }

            return validCount > 1 ? (last - first).normalized : Vector2.zero;
        }

        public Vector2 FirstActivePoint()
        {
            if (points.Length == 0) return Vector2.zero;

            foreach (var point in points)
            {
                var pos = point.position;
                if (pos != Vector3.zero)
                {
                    return point.position;
                }
            }

            return Vector2.zero;
        }
    }
    [Serializable]
    public struct ColorPoint
    {
        public Color32 color;
        public Vector3 position;

        public ColorPoint(Color color, Vector3 position)
        {
            this.color = color;
            this.position = position;
        }
    }

    public class PriorityAction
    {
        private readonly SortedList<int, List<Action>> _actions = new();

        public void Add(Action action, int priority)
        {
            if (!_actions.ContainsKey(priority))
                _actions[priority] = new List<Action>();

            _actions[priority].Add(action);
        }

        public void Remove(Action action)
        {
            foreach (var kv in _actions)
                kv.Value.Remove(action);
        }

        public void Invoke()
        {
            foreach (var kv in _actions)
            {
                foreach (var action in kv.Value)
                    action?.Invoke();
            }
        }
    }

}