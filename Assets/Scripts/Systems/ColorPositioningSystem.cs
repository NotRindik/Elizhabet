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
        private SpriteRenderer spriteRenderer;
        private Sprite lastSprite;
        private Texture2D texture;
        public void Initialize(Controller owner, ColorPositioningComponent colorPositioningComponent)
        {
            colorComponent = colorPositioningComponent;
            ownerTransform = owner.transform;
            spriteRenderer = owner.GetComponent<SpriteRenderer>();
            base.Initialize(owner);
        }

        public override void Update()
        {
            texture = spriteRenderer.sprite.texture;
            FindColorPositions();
        }

        private unsafe void FindColorPositions()
        {
            if (colorComponent == null || texture == null) return;

            Color32* pixels;
            int width = texture.width;
            int height = texture.height;
            Transform owner = ownerTransform;
            Vector3 ownerPos = owner.position;
            float scaleX = owner.localScale.x;
            float ownerRotY = owner.rotation.eulerAngles.y;

            // Вычисляем поворот один раз
            Quaternion rotation = Quaternion.Euler(0, ownerRotY + (scaleX < 0 ? 180f : 0), 0);

            // Получаем доступ к пикселям через указатели
            fixed (Color32* pixelPtr = texture.GetPixels32())
            {
                pixels = pixelPtr;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color32 pixelColor = pixels[y * width + x];

                        if (pixelColor.a == 0) continue;

                        // Линейный поиск нужного цвета (раз нельзя Dictionary)
                        for (int z = 0; z < colorComponent.points.Length; z++)
                        {
                            ref var point = ref colorComponent.points[z];

                            if (point.color.r == pixelColor.r &&
                                point.color.g == pixelColor.g &&
                                point.color.b == pixelColor.b &&
                                point.color.a == pixelColor.a)
                            {
                                Vector2 worldPos = PixelToWorldPosition(x, y, width, height);
                                Vector2 rotatedWorldPos = (Vector2)(rotation * (worldPos - (Vector2)ownerPos)) + (Vector2)ownerPos;
                                point.position = rotatedWorldPos;
                                break; // Прерываем поиск, если нашли цвет
                            }
                        }
                    }
                }
            }
        }

        private Vector2 PixelToWorldPosition(int x, int y, int texWidth, int texHeight)
        {
            Bounds bounds = spriteRenderer.bounds;

            // Центр спрайта в мировых координатах
            Vector2 worldCenter = bounds.center;

            // Размер спрайта в мире
            float worldWidth = bounds.size.x;
            float worldHeight = bounds.size.y;

            // Нормализуем координаты пикселя относительно текстуры (0.0 - 1.0)
            float normalizedX = x / (float)(texWidth - 1);
            float normalizedY = y / (float)(texHeight - 1);

            // Конвертируем в мировые координаты, центрируя относительно Pivot
            float worldX = worldCenter.x + (normalizedX - spriteRenderer.sprite.pivot.x / texWidth) * worldWidth;
            float worldY = worldCenter.y + (normalizedY - spriteRenderer.sprite.pivot.y / texHeight) * worldHeight;

            return new Vector2(worldX, worldY);
        }
    }

    [System.Serializable]
    public class ColorPositioningComponent : IComponent
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
                if (point.position == Vector3.zero) continue; // Игнорируем невидимые пиксели

                if (validCount == 0)
                {
                    first = point.position; // Первое нормальное значение
                }
                last = point.position; // Последнее нормальное значение
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