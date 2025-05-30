using Assets.Scripts;
using AYellowpaper.SerializedCollections;
using Controllers;
using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace Systems
{
    public class ColorPositioningSystem : BaseSystem
    {
        ColorPositioningComponent colorComponent;

        private Transform ownerTransform;
        private Texture2D texture;
        private Sprite lastSprite;

        private ComputeBuffer _textureData;
        private ComputeBuffer _targetColors;
        
        private int _kernelHandle;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            colorComponent = owner.GetControllerComponent<ColorPositioningComponent>();
            ownerTransform = owner.transform;
            
            colorComponent.colorPositioningShader = Object.Instantiate(colorComponent.colorPositioningShader);
            _kernelHandle = colorComponent.colorPositioningShader.FindKernel("ColorPositioning");
        }

        public override void OnUpdate()
        {
            texture = colorComponent.spriteRenderer.sprite.texture;
            FindColorPositions(colorComponent.spriteRenderer.sprite);
        }

        private unsafe void FindColorPositions(Sprite sprite)
        {
            if (colorComponent == null || texture == null) return;

            int width = texture.width;
            int height = texture.height;
            Transform owner = ownerTransform;
            Vector3 ownerPos = owner.position;
            float scaleX = owner.localScale.x;
            float ownerRotZ = owner.eulerAngles.z; // для 2D чаще всего важен Z

            // Создаем матрицу поворота вручную
            float angleRad = Mathf.Deg2Rad * (scaleX < 0 ? ownerRotZ + 180f : ownerRotZ);
            float cos = Mathf.Cos(angleRad);
            float sin = Mathf.Sin(angleRad);

            // Загружаем текстуру
            NativeArray<Color32> rawTextureData = texture.GetRawTextureData<Color32>();
            _textureData = new ComputeBuffer(rawTextureData.Length, Marshal.SizeOf<Color32>());
            _textureData.SetData(rawTextureData);

            var shader = colorComponent.colorPositioningShader;
            shader.SetBuffer(_kernelHandle, "rawTextureData", _textureData);
            shader.SetInt("_TextureWidth", width);
            shader.SetInt("_TextureHeight", height);
            shader.SetInts("_TextureSize", new int[] { width, height });

            shader.SetFloats("_OwnerPos", new float[] { ownerPos.x, ownerPos.y });
            shader.SetFloats("_RotationMatrix", new float[] {
                cos, -sin, // x, y
                sin,  cos  // z, w
            });

            foreach (var pointGroup in colorComponent.pointsGroup)
            {
                var points = pointGroup.Value.points;
                int length = points.Length;
                if (length == 0) continue;
                
                Color32[] colors = new Color32[length];
                for (int i = 0; i < length; i++)
                    colors[i] = points[i].color;
                // Буферы
                _targetColors = new ComputeBuffer(length, Marshal.SizeOf<Color32>());
                _targetColors.SetData(colors);
                ComputeBuffer worldPositions = new ComputeBuffer(length, sizeof(float) * 2);
                ComputeBuffer colorIndices = new ComputeBuffer(length, sizeof(int));

                shader.SetBuffer(_kernelHandle, "_TargetColors", _targetColors);
                shader.SetBuffer(_kernelHandle, "_WorldPositions", worldPositions);
                shader.SetBuffer(_kernelHandle, "_ColorIndices", colorIndices);
                shader.SetInt("_pointsLen", length);

                // Dispatch
                int threadGroupsX = Mathf.CeilToInt(width / 8.0f);
                int threadGroupsY = Mathf.CeilToInt(height / 8.0f);
                shader.Dispatch(_kernelHandle, threadGroupsX, threadGroupsY, 1);

                // Получение результатов
                Vector2[] resultPositions = new Vector2[length];
                for (int i = 0; i < length; i++) resultPositions[i] = Vector2.zero; // Явное обнуление

                int[] indices = new int[length];
                colorIndices.GetData(indices);
                worldPositions.GetData(resultPositions);

// Запись в точки, только если были найдены
                for (int i = 0; i < length; i++)
                {
                    if (indices[i] >= 0 && indices[i] < length)
                        points[indices[i]].position = resultPositions[indices[i]];
                    else
                        points[i].position = Vector2.zero; // Цвет не найден
                }

                // Очистка
                _targetColors.Dispose();
                worldPositions.Dispose();
                colorIndices.Dispose();
            }

            _textureData.Dispose();
            rawTextureData.Dispose();
        }


        private Vector2 PixelToWorldPosition(int x, int y, int texWidth, int texHeight)
        {
            Bounds bounds = colorComponent.spriteRenderer.bounds;

            Vector2 worldCenter = bounds.center;

            float worldWidth = bounds.size.x;
            float worldHeight = bounds.size.y;

            float normalizedX = x / (float)(texWidth - 1);
            float normalizedY = y / (float)(texHeight - 1);

            float worldX = worldCenter.x + (normalizedX - colorComponent.spriteRenderer.sprite.pivot.x / texWidth) * worldWidth;
            float worldY = worldCenter.y + (normalizedY - colorComponent.spriteRenderer.sprite.pivot.y / texHeight) * worldHeight;

            return new Vector2(worldX, worldY);
        }
    }

    [Serializable]
    public class ColorPositioningComponent : IComponent
    {
        public ComputeShader colorPositioningShader;
        public SpriteRenderer spriteRenderer;
        [SerializedDictionary] public SerializedDictionary<ColorPosNameConst, ColorPointGroup> pointsGroup = new SerializedDictionary<ColorPosNameConst, ColorPointGroup>();
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