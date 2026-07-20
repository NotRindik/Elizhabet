using AYellowpaper.SerializedCollections;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Systems
{
    public class ColorPositioningSystem : BaseSystem, IDisposable
    {
        ColorPositioningComponent colorComponent;

        private Dictionary<Color32, Vector2Int> cachedLocalPositions = new();
        private List<(Color32 color, SpriteRenderer sr)> _colorIndexMap = new();

        private List<JobHandle> _pendingJobs = new();
        private List<NativeArray<Color32>> _jobPixelsList = new();
        private NativeArray<Color32> _jobTargetColors;
        private NativeArray<int2> _jobResults;
        private bool _jobScheduled = false;

        private Dictionary<int, SpriteRenderer> _indexToRenderer = new();
        private List<NativeArray<int>> _jobIndicesList = new();
        
        private List<SpriteRenderer> _renderers = new();
        private Dictionary<SpriteRenderer, (List<int> colorIndices, Rect texRect, int texW, int texH)> _rendererGroups = new();

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            colorComponent = owner.GetControllerComponent<ColorPositioningComponent>();
            owner.OnLateUpdate += Update;
        }

        public override void OnUpdate()
        {
            if (_jobScheduled)
            {
                if (_pendingJobs.Count > 0)
                    _pendingJobs[_pendingJobs.Count - 1].Complete();

                _pendingJobs.Clear();
                _jobScheduled = false;

                for (int i = 0; i < _colorIndexMap.Count; i++)
                {
                    cachedLocalPositions[_colorIndexMap[i].color] = new Vector2Int(
                        _jobResults[i].x,
                        _jobResults[i].y
                    );
                }

                DisposeJobArrays();
            }

            UpdateWorldPositions();
            colorComponent.AfterColorCalculated.Invoke();
            ScheduleColorSearchJob();
        }

        public void ForceUpdatePosition(ColorPosNameConst[] keys)
        {
            if (colorComponent == null) return;

            foreach (var key in keys)
            {
                if (!colorComponent.pointsGroup.TryGetValue(key, out var group)) continue;

                var targetRenderer = group.searchingRenderer ?? colorComponent.spriteRenderer;

                for (int z = 0; z < group.points.Length; z++)
                {
                    ref var point = ref group.points[z];

                    if (cachedLocalPositions.TryGetValue(point.color, out var px) && px.x >= 0)
                        point.position = PixelToWorldPosition(px.x, px.y, targetRenderer);
                    else
                        point.position = Vector3.zero;
                }
            }
        }

        private void ScheduleColorSearchJob()
        {
            if (colorComponent == null) return;

            _colorIndexMap.Clear();
            _indexToRenderer.Clear();

            // Переиспользуем коллекции
            CollectRenderers();

            _rendererGroups.Clear();
            foreach (var sr in _renderers)
            {
                if (sr?.sprite == null) continue;
                _rendererGroups[sr] = (new List<int>(), sr.sprite.textureRect, sr.sprite.texture.width, sr.sprite.texture.height);
            }

            foreach (var pointGroup in colorComponent.pointsGroup)
            {
                var targetSr = pointGroup.Value.searchingRenderer ?? colorComponent.spriteRenderer;
                if (targetSr == null || !_rendererGroups.ContainsKey(targetSr)) continue;

                foreach (var point in pointGroup.Value.points)
                {
                    int idx = _colorIndexMap.Count;
                    _rendererGroups[targetSr].colorIndices.Add(idx);
                    _colorIndexMap.Add((point.color, targetSr));
                    _indexToRenderer[idx] = targetSr;
                }
            }

            if (_colorIndexMap.Count == 0) return;

            _jobTargetColors = new NativeArray<Color32>(_colorIndexMap.Count, Allocator.Persistent);
            _jobResults = new NativeArray<int2>(_colorIndexMap.Count, Allocator.Persistent);

            for (int i = 0; i < _colorIndexMap.Count; i++)
                _jobTargetColors[i] = _colorIndexMap[i].color;

            _pendingJobs.Clear();
            _jobPixelsList.Clear();
            _jobIndicesList.Clear();

            JobHandle previousHandle = default;
            bool anyJobScheduled = false;

            foreach (var (sr, (colorIndices, texRect, texW, texH)) in _rendererGroups)
            {
                if (colorIndices.Count == 0) continue;

                var rawPixels = sr.sprite.texture.GetRawTextureData<Color32>();
                var pixelsCopy = new NativeArray<Color32>(rawPixels, Allocator.Persistent);

                // Без ToArray() — копируем напрямую
                var nativeIndices = new NativeArray<int>(colorIndices.Count, Allocator.Persistent);
                for (int i = 0; i < colorIndices.Count; i++)
                    nativeIndices[i] = colorIndices[i];

                var job = new ColorSearchJob
                {
                    pixels       = pixelsCopy,
                    targetColors = _jobTargetColors,
                    results      = _jobResults,
                    indices      = nativeIndices,
                    width        = texW,
                    rectX        = (int)texRect.x,
                    rectY        = (int)texRect.y,
                    rectW        = (int)texRect.width,
                    rectH        = (int)texRect.height,
                };

                previousHandle = job.Schedule(colorIndices.Count, 1, previousHandle);
                _pendingJobs.Add(previousHandle);
                _jobPixelsList.Add(pixelsCopy);
                _jobIndicesList.Add(nativeIndices);
                anyJobScheduled = true;
            }

            if (!anyJobScheduled)
            {
                DisposeJobArrays();
                return;
            }

            _jobScheduled = true;
        }

        private void CollectRenderers()
        {
            _renderers.Clear();
            if (colorComponent.spriteRenderer != null)
            {
                _renderers.Add(colorComponent.spriteRenderer);
            }
            else
            {
                foreach (var group in colorComponent.pointsGroup.Values)
                    if (group.searchingRenderer != null)
                        _renderers.Add(group.searchingRenderer);
            }
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
                        point.position = PixelToWorldPosition(px.x, px.y, targetRenderer);
                    else
                        point.position = Vector3.zero;
                }
            }
        }

        private Vector3 PixelToWorldPosition(int x, int y, SpriteRenderer sr)
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

        private void DisposeJobArrays()
        {
            foreach (var arr in _jobPixelsList)
                if (arr.IsCreated) arr.Dispose();
            _jobPixelsList.Clear();

            foreach (var arr in _jobIndicesList)
                if (arr.IsCreated) arr.Dispose();
            _jobIndicesList.Clear();

            if (_jobTargetColors.IsCreated) _jobTargetColors.Dispose();
            if (_jobResults.IsCreated) _jobResults.Dispose();
        }

        public void Dispose()
        {
            if (_jobScheduled)
            {
                if (_pendingJobs.Count > 0)
                    _pendingJobs[_pendingJobs.Count - 1].Complete();
                _pendingJobs.Clear();
                _jobScheduled = false;
            }
            DisposeJobArrays();
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
                if (validCount == 0) first = point.position;
                last = point.position;
                validCount++;
            }

            return validCount > 1 ? (last - first).normalized : Vector2.zero;
        }

        public Vector2 FirstActivePoint()
        {
            if (points.Length == 0) return Vector2.zero;

            foreach (var point in points)
                if (point.position != Vector3.zero)
                    return point.position;

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
        private readonly Dictionary<Action, int> _reverse = new();

        public void Add(Action action, int priority)
        {
            if (!_actions.TryGetValue(priority, out var list))
            {
                list = new List<Action>(4);
                _actions.Add(priority, list);
            }
            list.Add(action);
            _reverse[action] = priority;
        }

        public void Remove(Action action)
        {
            if (!_reverse.TryGetValue(action, out var priority)) return;

            var list = _actions[priority];
            int index = list.IndexOf(action);
            if (index >= 0)
            {
                int last = list.Count - 1;
                list[index] = list[last];
                list.RemoveAt(last);
            }
            _reverse.Remove(action);
        }

        public void Invoke()
        {
            var values = _actions.Values;
            for (int i = 0; i < values.Count; i++)
            {
                var list = values[i];
                for (int j = 0, count = list.Count; j < count; j++)
                    list[j]();
            }
        }
    }

    public unsafe struct FastAction
    {
        public void* target;
        public delegate*<void*, void> method;

        public void Invoke() => method(target);
    }
}