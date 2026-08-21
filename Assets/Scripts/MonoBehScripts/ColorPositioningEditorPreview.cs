#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Systems
{
    // Эдиторная обвязка над ColorPositioningComponent.
    //
    // ColorPositioningSystem сам по себе не дёргается в Edit Mode: Initialize()
    // требует AbstractEntity/BaseSystem, которых вне рантайма нет, а OnUpdate()
    // рассчитан на то, что его каждый кадр вызывает игровой цикл системы, плюс
    // сама схема через Job System заточена под то, что кто-то раз в кадр делает
    // Complete(). Здесь — синхронная версия того же пересчёта (без Job'ов, они
    // тут не нужны — это разовый вызов по кнопке, а не цикл 60 раз в секунду)
    // плюс превью на реальных Transform'ах с гарантированным откатом.
    //
    // Превью НЕ использует AnimationMode — в отличие от записи анимации (где мы
    // не знаем заранее, что именно поменяет пользователь, и должны ловить любые
    // правки), тут мы сами точно знаем, что и на сколько двигаем. Поэтому проще
    // и надёжнее: запомнить исходную world-позицию каждого Transform руками и
    // вернуть её руками в EditorUpdate_StopPreview(). Так же на всякий случай
    // подстрахованы OnDisable/OnDestroy — если объект/компонент снесут прямо
    // во время активного превью, позиции всё равно откатятся.
    //
    // Методы с префиксом EditorUpdate_ — дёргать руками снаружи (кастомный
    // инспектор, [ContextMenu], кнопка в EditorWindow и т.п.). Сам этот класс
    // ничего по таймеру/Update() не вызывает.
    [ExecuteAlways]
    public class ColorPositioningEditorPreview : MonoBehaviour
    {
        [SerializeField] private ColorPositioningComponent colorComponent;

        [Serializable]
        public struct PreviewTarget
        {
            public Color32  color;   // должен совпадать с ColorPoint.color в pointsGroup
            public Transform target; // "тело" — что физически двигаем ради превью
        }

        [Tooltip("Какие ColorPoint.color на какие Transform-ы проецировать для визуального превью")]
        [SerializeField] private List<PreviewTarget> previewTargets = new();

        private readonly Dictionary<Color32, Vector2Int> _cachedLocalPositions = new();

        private bool _previewActive;
        private readonly Dictionary<Transform, Vector3> _originalWorldPositions = new();

        // ═══════════════════════════════════════════════════
        // EditorUpdate — дёргать руками, в таком порядке:
        // 1) EditorUpdate_RecalculatePositions()
        // 2) EditorUpdate_ApplyPreview()
        // ... смотрим глазами ...
        // 3) EditorUpdate_StopPreview()
        // ═══════════════════════════════════════════════════

        // EditorUpdate: синхронный поиск пикселей по всем pointsGroup, без Job System.
        // Заполняет _cachedLocalPositions и проставляет ColorPoint.position — это чистые
        // данные внутри colorComponent (не Transform), поэтому их спокойно можно менять
        // в Edit Mode: они нигде не сериализуются на сцену как "изменение объекта".
        public void EditorUpdate_RecalculatePositions()
        {
            if (colorComponent == null) return;
            _cachedLocalPositions.Clear();

            foreach (var pointGroup in colorComponent.pointsGroup)
            {
                var targetSr = pointGroup.Value.searchingRenderer ?? colorComponent.spriteRenderer;
                if (targetSr == null || targetSr.sprite == null) continue;

                var tex = targetSr.sprite.texture;
                if (!tex.isReadable)
                {
                    Debug.LogWarning(
                        $"[{nameof(ColorPositioningEditorPreview)}] Текстура '{tex.name}' не Read/Write — " +
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
                    _cachedLocalPositions[color] = FindColorInRect(pixels, texW, rx, ry, rw, rh, color);
                }
            }

            PushCachedPositionsIntoPoints();
        }

        // EditorUpdate: применяет посчитанные позиции к previewTargets. Реальные
        // Transform'ы двигаются визуально прямо на сцене, но ничего не идёт через
        // Undo и не помечается как "сохранить" — при EditorUpdate_StopPreview()
        // всё возвращается побитово к тому, что было.
        public void EditorUpdate_ApplyPreview()
        {
            if (colorComponent == null) return;

            foreach (var pt in previewTargets)
            {
                if (pt.target == null) continue;
                if (!_cachedLocalPositions.TryGetValue(pt.color, out var px) || px.x < 0) continue;
                if (!TryFindRendererForColor(pt.color, out var renderer)) continue;

                // Оригинал запоминаем только один раз за сессию превью — повторные
                // вызовы ApplyPreview (например после правки картинки) не должны
                // затереть его уже сдвинутым значением.
                if (!_originalWorldPositions.ContainsKey(pt.target))
                    _originalWorldPositions[pt.target] = pt.target.position;

                pt.target.position = PixelToWorldPosition(px.x, px.y, renderer);
            }

            _previewActive = true;
#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif
        }

        // EditorUpdate: выключает превью, откатывает все previewTargets к исходным
        // мировым позициям, которые были запомнены перед первым ApplyPreview.
        public void EditorUpdate_StopPreview()
        {
            if (!_previewActive) return;

            foreach (var kv in _originalWorldPositions)
                if (kv.Key != null) kv.Key.position = kv.Value;

            _originalWorldPositions.Clear();
            _previewActive = false;
#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif
        }

        // Страховка: если объект выключат/удалят прямо во время активного превью,
        // всё равно откатываем — иначе сдвинутые позиции так и останутся в сцене.
        private void OnDisable() => EditorUpdate_StopPreview();
        private void OnDestroy() => EditorUpdate_StopPreview();

        // ═══════════════════════════════════════════════════
        // ВНУТРЕННЕЕ — копия логики из ColorPositioningSystem,
        // но синхронно и без Job System
        // ═══════════════════════════════════════════════════

        private void PushCachedPositionsIntoPoints()
        {
            foreach (var pointGroup in colorComponent.pointsGroup)
            {
                var targetRenderer = pointGroup.Value.searchingRenderer ?? colorComponent.spriteRenderer;
                if (targetRenderer == null) continue;

                for (int i = 0; i < pointGroup.Value.points.Length; i++)
                {
                    ref var point = ref pointGroup.Value.points[i];
                    if (_cachedLocalPositions.TryGetValue(point.color, out var px) && px.x >= 0)
                        point.position = PixelToWorldPosition(px.x, px.y, targetRenderer);
                    else
                        point.position = Vector3.zero;
                }
            }
        }

        private bool TryFindRendererForColor(Color32 color, out SpriteRenderer renderer)
        {
            foreach (var pointGroup in colorComponent.pointsGroup)
            {
                foreach (var point in pointGroup.Value.points)
                {
                    if (!point.color.Equals(color)) continue;
                    renderer = pointGroup.Value.searchingRenderer ?? colorComponent.spriteRenderer;
                    return renderer != null;
                }
            }
            renderer = null;
            return false;
        }

        // Точная копия семантики ColorSearchJob (Burst-джоба, который реально
        // гоняется в рантайме): пропускаем полностью прозрачные пиксели,
        // сравниваем только r/g/b (альфа НЕ участвует в сравнении с target),
        // возвращаем АБСОЛЮТНЫЕ координаты в текстуре — не локальные внутри
        // rect'а спрайта. (-1,-1), если не нашли.
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

        // Дословная копия ColorPositioningSystem.PixelToWorldPosition — держи её
        // синхронно, если поправишь одну, поправь и вторую (или вынеси в общий
        // static-класс, чтобы не разъезжались).
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
