using Assets.Scripts;
using AYellowpaper.SerializedCollections;
using Controllers;
using System;
using System.Collections;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Systems
{
    public class ColorPositioningSystem : BaseSystem, IDisposable
    {
        ColorPositioningComponent colorComponent;

        private Transform ownerTransform;
        private Sprite lastSprite;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            colorComponent = owner.GetControllerComponent<ColorPositioningComponent>();
            ownerTransform = owner.transform;

            owner.StartCoroutine(ScanAfterAnimator());
        }
        public IEnumerator ScanAfterAnimator()
        {
            while (true)
            {
                yield return new WaitForEndOfFrame();
                Update();
            }
        }
        public override void OnUpdate()
        {
            FindColorPositions();
        }

        private unsafe void FindColorPositions()
        {
            if (colorComponent == null) return;
           
            
            foreach (var pointGroup in colorComponent.pointsGroup)
            {
                var sr = pointGroup.Value.searchingRenderer;

                var texture = sr != null ? 
                    sr.sprite.texture 
                    : null;
                if (texture == null) texture = colorComponent.spriteRenderer.sprite.texture;

                Rect rect = pointGroup.Value.searchingRenderer.sprite.textureRect;

                int xMin = Mathf.FloorToInt(rect.x);
                int yMin = Mathf.FloorToInt(rect.y);
                int xMax = xMin + Mathf.FloorToInt(rect.width);
                int yMax = yMin + Mathf.FloorToInt(rect.height);

                Transform owner = ownerTransform;
                Vector3 ownerPos = owner.position;
                float scaleX = owner.localScale.x;
                float ownerRotY = owner.rotation.eulerAngles.y;

                NativeArray<Color32> rawTextureData = texture.GetRawTextureData<Color32>();
                Color32* pixelPtr = (Color32*)NativeArrayUnsafeUtility.GetUnsafePtr(rawTextureData);

                Quaternion rotation = Quaternion.Euler(0, ownerRotY + (scaleX < 0 ? 180f : 0), 0);

                bool[] colorFound = new bool[pointGroup.Value.points.Length];

                for (int y = yMin; y < yMax; y++)
                {
                    for (int x = xMin; x < xMax; x++)
                    {
                        Color32 pixelColor = *(pixelPtr + (y * texture.width + x));

                        if (pixelColor.a == 0) continue;

                        for (int z = 0; z < pointGroup.Value.points.Length; z++)
                        {
                            ref var point = ref pointGroup.Value.points[z];
                            
                            if (point.color.r == pixelColor.r && point.color.g == pixelColor.g && point.color.b == pixelColor.b)
                            {
                                Vector2 worldPos = PixelToWorldPosition(x, y, sr);

                                //Vector2 rotatedWorldPos = (Vector2)(rotation * (worldPos - (Vector2)ownerPos)) + (Vector2)ownerPos;
                                point.position = worldPos;
                                colorFound[z] = true; 
                                break;
                            }
                        }
                    }
                }
                rawTextureData.Dispose();
                for (int z = 0; z < pointGroup.Value.points.Length; z++)
                {
                    if (!colorFound[z])
                    {
                        pointGroup.Value.points[z].position = Vector2.zero;
                    }
                }   
            }

            colorComponent.AfterColorCalculated?.Invoke();
        }

        private Vector3 PixelToWorldPosition(int texX, int texY, SpriteRenderer sr)
        {
            var sprite = sr.sprite;
            float ppu = sprite.pixelsPerUnit;

            Rect texRect = sprite.textureRect;   // прямоугольник спрайта в атласе
            Vector2 pivotPx = sprite.pivot;

            // локальные координаты внутри rect
            float sx = texX - texRect.x;
            float sy = texY - texRect.y;

            float dxPx = sx + 0.5f - pivotPx.x;
            float dyPx = sy + 0.5f - pivotPx.y;

            Vector2 local = new Vector2(dxPx / ppu, dyPx / ppu);
            Debug.DrawLine(sr.transform.position, sr.transform.TransformPoint(local), Color.red, 5f);

            if (sr.drawMode != SpriteDrawMode.Simple)
            {
                Vector2 spriteWorldSize = sprite.rect.size / ppu;
                Vector2 targetSize = sr.size;
                if (spriteWorldSize.x != 0f) local.x *= targetSize.x / spriteWorldSize.x;
                if (spriteWorldSize.y != 0f) local.y *= targetSize.y / spriteWorldSize.y;
            }

            return sr.transform.TransformPoint(local);
        }



        public void Dispose()
        {
            owner.StopCoroutine(ScanAfterAnimator());
        }
    }

    [Serializable]
    public class ColorPositioningComponent : IComponent
    {
        public SpriteRenderer spriteRenderer;
        [SerializedDictionary] public SerializedDictionary<ColorPosNameConst, ColorPointGroup> pointsGroup = new SerializedDictionary<ColorPosNameConst, ColorPointGroup>();
        public Action AfterColorCalculated;
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
}