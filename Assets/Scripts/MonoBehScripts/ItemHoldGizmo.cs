#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Systems
{
    public enum ItemHoldMode
    {
        OneHand,
        OneHandAlongArm,
        TwoHand
    }

    public enum ItemLongAxis
    {
        X,
        Y,
        NegX,
        NegY
    }

    [Serializable]
    public class ItemHoldPreset
    {
        public string name = "Item";
        public bool enabled = true;
        public ItemHoldMode mode;
        public ItemLongAxis longAxis = ItemLongAxis.X;
        [Min(0.01f)] public float length = 0.8f;
        [Min(0.005f)] public float radius = 0.06f;
        [Range(0f, 1f)] public float grip = 0.15f;
        public Color color = new(1f, 0.6f, 0.2f, 1f);
    }

    [ExecuteAlways]
    public class ItemHoldGizmo : MonoBehaviour
    {
        private const int ArcSegments = 10;

        [SerializeField] private ColorPositioningPreview colorPreview;

        [Tooltip("Рисовать в плоскости спрайта кисти: капсула сжимается при 3D-поворотах руки. Выключено — плоская математика как в рантайме.")]
        [SerializeField] private bool handPlane3D = true;

        [SerializeField] private bool onlyWhenSelected;

        [Tooltip("Подпись видна, только если капсула на экране длиннее этого числа пикселей")]
        [SerializeField, Min(0f)] private float labelMinPixels = 80f;

        [SerializeField] private List<ItemHoldPreset> presets = new()
        {
            new ItemHoldPreset { name = "Одноручное", mode = ItemHoldMode.OneHand, longAxis = ItemLongAxis.X, length = 0.8f, radius = 0.06f, grip = 0.15f, color = new Color(1f, 0.6f, 0.2f) },
            new ItemHoldPreset { name = "Вдоль руки", mode = ItemHoldMode.OneHandAlongArm, longAxis = ItemLongAxis.Y, length = 0.6f, radius = 0.05f, grip = 0.2f, color = new Color(0.3f, 0.85f, 1f), enabled = false },
            new ItemHoldPreset { name = "Двуручное", mode = ItemHoldMode.TwoHand, longAxis = ItemLongAxis.Y, length = 1.4f, radius = 0.07f, grip = 0.3f, color = new Color(0.95f, 0.4f, 0.9f), enabled = false }
        };

        private readonly List<Vector2> _right = new();
        private readonly List<Vector2> _left = new();
        private readonly List<Vector3> _outline = new();

        private void OnValidate()
        {
            if (colorPreview == null)
                colorPreview = GetComponentInChildren<ColorPositioningPreview>(true);
            if (colorPreview == null)
                colorPreview = GetComponentInParent<ColorPositioningPreview>(true);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!onlyWhenSelected)
                DrawAll();
        }

        private void OnDrawGizmosSelected()
        {
            if (onlyWhenSelected)
                DrawAll();
        }

        private void DrawAll()
        {
            if (colorPreview == null || presets == null)
                return;

            foreach (var preset in presets)
                if (preset != null && preset.enabled && TryComputeFrame(preset, out var frame))
                    DrawCapsule(preset, frame);
        }
#endif

        private struct Frame
        {
            public Vector2 Origin;
            public float Angle;
            public float YScale;
            public Transform Space;
            public float Z;
        }

        private bool TryComputeFrame(ItemHoldPreset preset, out Frame frame)
        {
            frame = default;
            if (!colorPreview.TryGetGroupPoints(ColorPosNameConst.RIGHT_HAND_POS, _right, out var rightSr))
                return false;

            bool hasLeft = preset.mode == ItemHoldMode.TwoHand &&
                           colorPreview.TryGetGroupPoints(ColorPosNameConst.LEFT_HAND, _left, out var leftSr) &&
                           ToSpace(_left, leftSr, rightSr);
            if (preset.mode == ItemHoldMode.TwoHand && !hasLeft)
                _left.Clear();

            var owner = colorPreview.Component != null ? colorPreview.transform : transform;
            float facing = handPlane3D ? 1f : (owner.right.x < 0f ? -1f : 1f);

            if (!handPlane3D)
                for (int i = 0; i < _right.Count; i++)
                    _right[i] = rightSr.transform.TransformPoint(_right[i]);

            frame.Space = handPlane3D ? rightSr.transform : null;
            frame.Z = rightSr.transform.position.z;
            frame.Origin = _right[0];

            Vector2 handDir = _right.Count > 1 ? -(_right[^1] - _right[0]).normalized : Vector2.zero;
            float handAngle = Mathf.Atan2(handDir.y, handDir.x) * Mathf.Rad2Deg;

            switch (preset.mode)
            {
                case ItemHoldMode.OneHand:
                    frame.Angle = handAngle;
                    frame.YScale = facing;
                    break;

                case ItemHoldMode.OneHandAlongArm:
                    frame.Angle = handAngle - 90f;
                    frame.YScale = 1f;
                    break;

                case ItemHoldMode.TwoHand:
                    if (_left.Count == 0)
                    {
                        frame.Angle = handAngle;
                    }
                    else
                    {
                        var along = (frame.Origin - _left[0]) * facing;
                        frame.Angle = Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg + 90f;
                    }
                    frame.YScale = facing;
                    break;
            }

            return true;
        }

        private bool ToSpace(List<Vector2> points, SpriteRenderer from, SpriteRenderer rightSr)
        {
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 world = from.transform.TransformPoint(points[i]);
                points[i] = handPlane3D ? (Vector2)rightSr.transform.InverseTransformPoint(world) : (Vector2)world;
            }
            return points.Count > 0;
        }

#if UNITY_EDITOR
        private void DrawCapsule(ItemHoldPreset preset, Frame frame)
        {
            float rad = frame.Angle * Mathf.Deg2Rad;
            var ax = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            var ay = new Vector2(-ax.y, ax.x) * frame.YScale;

            Vector2 u = preset.longAxis switch
            {
                ItemLongAxis.X => ax,
                ItemLongAxis.NegX => -ax,
                ItemLongAxis.Y => ay,
                _ => -ay
            };
            Vector2 side = preset.longAxis is ItemLongAxis.X or ItemLongAxis.NegX ? ay : ax;
            Vector2 w = new Vector2(-u.y, u.x);

            float r = preset.radius;
            Vector2 a = frame.Origin - u * (preset.grip * preset.length);
            Vector2 b = frame.Origin + u * ((1f - preset.grip) * preset.length);

            _outline.Clear();
            AddArc(frame, b, u, w, r);
            AddArc(frame, a, -u, -w, r);

            var fill = preset.color;
            fill.a = 0.18f;
            var line = preset.color;
            line.a = 0.95f;

            var polygon = _outline.ToArray();
            Handles.color = fill;
            Handles.DrawAAConvexPolygon(polygon);

            var closed = new Vector3[polygon.Length + 1];
            polygon.CopyTo(closed, 0);
            closed[^1] = polygon[0];
            Handles.color = line;
            Handles.DrawAAPolyLine(2f, closed);

            Handles.DrawDottedLine(Map(frame, a), Map(frame, b), 3f);

            var grip = new Vector3[17];
            for (int i = 0; i < grip.Length; i++)
            {
                float t = i / 16f * Mathf.PI * 2f;
                grip[i] = Map(frame, frame.Origin + (u * Mathf.Cos(t) + w * Mathf.Sin(t)) * (r * 0.55f));
            }
            Handles.DrawAAPolyLine(2f, grip);

            Vector2 mid = (a + b) * 0.5f;
            Handles.DrawAAPolyLine(3f, Map(frame, mid + side * r), Map(frame, mid + side * (r * 2.2f)));

            float screenLength = Vector2.Distance(
                HandleUtility.WorldToGUIPoint(Map(frame, a)),
                HandleUtility.WorldToGUIPoint(Map(frame, b)));
            if (screenLength >= labelMinPixels)
                Handles.Label(Map(frame, b + u * (r * 1.5f)), preset.name);
        }

        private void AddArc(Frame frame, Vector2 center, Vector2 forward, Vector2 left, float radius)
        {
            for (int i = 0; i <= ArcSegments; i++)
            {
                float t = Mathf.Lerp(-90f, 90f, i / (float)ArcSegments) * Mathf.Deg2Rad;
                _outline.Add(Map(frame, center + (forward * Mathf.Cos(t) - left * Mathf.Sin(t)) * radius));
            }
        }

        private static Vector3 Map(Frame frame, Vector2 p) =>
            frame.Space != null ? frame.Space.TransformPoint(p) : new Vector3(p.x, p.y, frame.Z);
#endif
    }
}
