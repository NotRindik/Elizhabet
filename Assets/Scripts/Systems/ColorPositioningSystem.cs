using Controllers;
using System.Collections.Generic;
using UnityEngine;

namespace Systems
{
    public class ColorPositioningSystem : BaseSystem
    {
        ColorPositioningComponent colorComponent;

        private Transform ownerTransform;
        private SpriteRenderer spriteRenderer;
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

        private void FindColorPositions()
        {
            if (colorComponent == null || texture == null) return;

            Color32[] pixels = texture.GetPixels32();
            int width = texture.width;
            int height = texture.height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color32 pixelColor = pixels[y * width + x];

                    if (pixelColor.a == 0) continue;
                    for (int z = 0; z < colorComponent.colorKey.Count; z++)
                    {
                        if (colorComponent.colorKey[z].Equals(pixelColor))
                        {
                            Vector2 worldPos = PixelToWorldPosition(x, y, width, height);

                            // Поворот с учётом флипа по scale.x
                            Quaternion rotation = Quaternion.Euler(0, ownerTransform.rotation.eulerAngles.y, 0);

                            // Проверка флипа по scale.x и инвертируем поворот, если scale.x < 0
                            if (ownerTransform.localScale.x < 0)
                            {
                                rotation = Quaternion.Euler(0, ownerTransform.rotation.eulerAngles.y + 180f, 0);
                            }

                            // Применяем поворот с учётом флипа и позиции
                            Vector2 rotatedWorldPos = (Vector2)(rotation * (worldPos - (Vector2)ownerTransform.position)) + (Vector2)ownerTransform.position;
                            colorComponent.vectorValue[z] = rotatedWorldPos;
                        }

                    }
                }
            }
        }

        private Vector2 PixelToWorldPosition(int x, int y, int texWidth, int texHeight)
        {
            Bounds bounds = spriteRenderer.bounds;
            Vector2 min = bounds.min; // Нижняя левая точка

            float worldX = min.x + (x / (float)texWidth) * bounds.size.x;
            float worldY = min.y + (y / (float)texHeight) * bounds.size.y;

            return new Vector2(worldX, worldY);
        }
    }

    [System.Serializable]
    public class ColorPositioningComponent:IComponent
    {
        public List<Color32> colorKey;
        public List<Vector2> vectorValue;
        public Vector2 direction => GetDirection();

        private Vector2 GetDirection()
        {
            return vectorValue[1] - vectorValue[0];
        }
    }
}