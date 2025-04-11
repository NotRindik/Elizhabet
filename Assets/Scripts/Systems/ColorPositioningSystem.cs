using Controllers;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Systems
{
    public class ColorPositioningSystem : BaseSystem
    {
        ColorPositioningComponent colorComponent;

        private Transform ownerTransform;
        private SpriteRenderer _spriteRenderer;
        private Texture2D texture;
        private Sprite lastSprite;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            colorComponent = owner.GetControllerComponent<ColorPositioningComponent>();
            ownerTransform = owner.transform;
            _spriteRenderer = owner.GetComponent<SpriteRenderer>();
        }

        public override void Update()
        {
            texture = _spriteRenderer.sprite.texture;
            FindColorPositions(_spriteRenderer.sprite);
        }

        private unsafe void FindColorPositions(Sprite sprite)
        {
            if (colorComponent == null || texture == null) return;

            Color32* pixels;
            int width = texture.width;
            int height = texture.height;
            Transform owner = ownerTransform;
            Vector3 ownerPos = owner.position;
            float scaleX = owner.localScale.x;
            float ownerRotY = owner.rotation.eulerAngles.y; // Исправлено на eulerAngles

            Quaternion rotation = Quaternion.Euler(0, ownerRotY + (scaleX < 0 ? 180f : 0), 0);

            // Массив для отслеживания, был ли найден цвет для каждой точки

            foreach (var pointGroup in colorComponent.pointsGroup)
            {
                bool[] colorFound = new bool[pointGroup.points.Length];

                fixed (Color32* pixelPtr = texture.GetPixels32())
                {
                    pixels = pixelPtr;

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            Color32 pixelColor = pixels[y * width + x];

                            if (pixelColor.a == 0) continue;

                            for (int z = 0; z < pointGroup.points.Length; z++)
                            {
                                ref var point = ref pointGroup.points[z];

                                if (point.color.r == pixelColor.r && point.color.g == pixelColor.g && point.color.b == pixelColor.b)
                                {
                                    Vector2 worldPos = PixelToWorldPosition(x, y, width, height);
                                    Vector2 rotatedWorldPos = (Vector2)(rotation * (worldPos - (Vector2)ownerPos)) + (Vector2)ownerPos;
                                    point.position = rotatedWorldPos;
                                    colorFound[z] = true; // Цвет найден
                                    break;
                                }
                            }
                        }
                    }
                }

                // Обнуляем позиции для точек, цвет которых не был найден
                for (int z = 0; z < pointGroup.points.Length; z++)
                {
                    if (!colorFound[z])
                    {
                        pointGroup.points[z].position = Vector2.zero;
                    }
                }   
            }
        }

        private Vector2 PixelToWorldPosition(int x, int y, int texWidth, int texHeight)
        {
            Bounds bounds = _spriteRenderer.bounds;

            Vector2 worldCenter = bounds.center;

            float worldWidth = bounds.size.x;
            float worldHeight = bounds.size.y;

            float normalizedX = x / (float)(texWidth - 1);
            float normalizedY = y / (float)(texHeight - 1);

            float worldX = worldCenter.x + (normalizedX - _spriteRenderer.sprite.pivot.x / texWidth) * worldWidth;
            float worldY = worldCenter.y + (normalizedY - _spriteRenderer.sprite.pivot.y / texHeight) * worldHeight;

            return new Vector2(worldX, worldY);
        }
    }

    [Serializable]
    public class ColorPositioningComponent : IComponent
    {
        public ColorPointGroup[] pointsGroup;
    }
    
    [Serializable]
    public struct ColorPointGroup
    {
        public ColorPoint[] points;
        public Vector2 direction => GetDirection();

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
}