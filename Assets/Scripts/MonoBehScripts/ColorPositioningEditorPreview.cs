#if UNITY_EDITOR
using UnityEditor;
#endif
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using Controllers;
using UnityEngine;

namespace Systems
{
    [ExecuteAlways]
    public class ColorPositioningPreview : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;

#if ODIN_INSPECTOR
        [Space]
        [LabelText("Work / Unwork")]
        [OnValueChanged(nameof(OnWorkToggled))]
#endif
        [SerializeField] private bool work;

        private bool _previewActive;

        private ColorPositioningComponent ColorComponent =>
            playerController != null
                ? playerController.colorPositioningComponent
                : null;

#if !ODIN_INSPECTOR
        // Без Odin тумблер дергаем через OnValidate — реагирует на изменение
        // галочки в инспекторе так же, как OnValueChanged у Odin.
        private bool _lastWork;
        private void OnValidate()
        {
            if (work == _lastWork) return;
            _lastWork = work;
            OnWorkToggled();
        }
#endif

        private void OnWorkToggled()
        {
            if (work) StartWorking();
            else StopWorking();
        }

        private void StartWorking()
        {
            if (ColorComponent == null)
            {
                Debug.LogWarning($"[{nameof(ColorPositioningPreview)}] Не назначен PlayerController или на нём нет ColorPositioningComponent.", this);
                work = false;
                return;
            }

            _previewActive = true;
            RecalculatePositions();
        }

        private void StopWorking()
        {
            _previewActive = false;
        }

        private void Update()
        {
            if (!work || !_previewActive || ColorComponent == null) return;

            RecalculatePositions();
        }

        private void OnDisable() => StopWorking();
        private void OnDestroy() => StopWorking();

        private void RecalculatePositions()
        {
            var colorComponent = ColorComponent;
            if (colorComponent == null) return;

            var cachedLocalPositions = new System.Collections.Generic.Dictionary<Color32, Vector2Int>();

            foreach (var pointGroup in colorComponent.pointsGroup)
            {
                var targetSr = pointGroup.Value.searchingRenderer ?? colorComponent.spriteRenderer;
                if (targetSr == null || targetSr.sprite == null) continue;

                var tex = targetSr.sprite.texture;
                if (!tex.isReadable)
                {
                    Debug.LogWarning(
                        $"[{nameof(ColorPositioningPreview)}] Текстура '{tex.name}' не Read/Write — " +
                        "включи Read/Write Enabled в Import Settings, иначе GetPixels32 в эдиторе кинет исключение.",
                        tex);
                    continue;
                }

                var rect = targetSr.sprite.textureRect;
                int rx = (int)rect.x, ry = (int)rect.y, rw = (int)rect.width, rh = (int)rect.height;
                Color32[] pixels = tex.GetPixels32();
                int texW = tex.width;

                for (int i = 0; i < pointGroup.Value.points.Length; i++)
                {
                    var color = pointGroup.Value.points[i].color;
                    if (!cachedLocalPositions.ContainsKey(color))
                        cachedLocalPositions[color] = FindColorInRect(pixels, texW, rx, ry, rw, rh, color);
                }
            }

            PushCachedPositionsIntoPoints(colorComponent, cachedLocalPositions);

#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif
            colorComponent.AfterColorCalculated.Invoke();
        }

        private void PushCachedPositionsIntoPoints(
            ColorPositioningComponent colorComponent,
            System.Collections.Generic.Dictionary<Color32, Vector2Int> cachedLocalPositions)
        {
            foreach (var pointGroup in colorComponent.pointsGroup)
            {
                var targetRenderer = pointGroup.Value.searchingRenderer ?? colorComponent.spriteRenderer;
                if (targetRenderer == null) continue;

                for (int i = 0; i < pointGroup.Value.points.Length; i++)
                {
                    ref var point = ref pointGroup.Value.points[i];
                    if (cachedLocalPositions.TryGetValue(point.color, out var px) && px.x >= 0)
                        point.position = PixelToWorldPosition(px.x, px.y, targetRenderer);
                    else
                        point.position = Vector3.zero;
                }
            }
        }

        private static Vector2Int FindColorInRect(
            Color32[] pixels, int texW, int rectX, int rectY, int rectW, int rectH, Color32 target)
        {
            for (int y = rectY; y < rectY + rectH; y++)
            {
                int row = y * texW;
                for (int x = rectX; x < rectX + rectW; x++)
                {
                    var p = pixels[row + x];
                    if (p.a == 0) continue;
                    if (p.r == target.r && p.g == target.g && p.b == target.b)
                        return new Vector2Int(x, y);
                }
            }
            return new Vector2Int(-1, -1);
        }

        private static Vector3 PixelToWorldPosition(int x, int y, SpriteRenderer sr)
        {
            var sprite = sr.sprite;
            float ppu = sprite.pixelsPerUnit;

            Vector2 rectSizePx = sprite.rect.size;
            Vector2 pivotPx = sprite.pivot;

            float dxPx = x + 0.5f - pivotPx.x;
            float dyPx = y + 0.5f - pivotPx.y;

            Vector2 local = new Vector2(dxPx / ppu, dyPx / ppu);

            if (sr.drawMode != SpriteDrawMode.Simple)
            {
                Vector2 spriteWorldSize = rectSizePx / ppu;
                Vector2 targetSize = sr.size;
                if (spriteWorldSize.x != 0f) local.x *= targetSize.x / spriteWorldSize.x;
                if (spriteWorldSize.y != 0f) local.y *= targetSize.y / spriteWorldSize.y;
            }

            return sr.transform.TransformPoint(local);
        }
    }
}