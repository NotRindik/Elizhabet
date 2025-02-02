using Common;
using Controllers;
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

            colorComponent.points.Clear(); // Очищаем старые данные

            Color32[] pixels = texture.GetPixels32();
            int width = texture.width;
            int height = texture.height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color32 pixelColor = pixels[y * width + x];

                    if (pixelColor.a == 0) continue;

                    if (colorComponent.points.ContainsKey(pixelColor)) 
                    {
                        Vector2 worldPos = PixelToWorldPosition(x, y, width, height);
                        colorComponent.points[pixelColor] = worldPos;
                    };
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
        public SerializedDictionary<Color32,Vector2> points;
    }
}