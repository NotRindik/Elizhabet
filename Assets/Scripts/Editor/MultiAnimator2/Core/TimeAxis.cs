using System;
using UnityEngine;

namespace MultiAnimator2
{
    public sealed class TimeAxis
    {
        public const float MinPixelsPerSecond = 20f;
        public const float MaxPixelsPerSecond = 8000f;

        public float PixelsPerSecond { get; private set; } = 300f;
        public float Scroll { get; private set; }

        public event Action Changed;

        public float T2X(float time) => time * PixelsPerSecond - Scroll;

        public float X2T(float x) => (x + Scroll) / PixelsPerSecond;

        public void SetScroll(float value)
        {
            value = Mathf.Max(0f, value);
            if (Mathf.Approximately(value, Scroll))
                return;
            Scroll = value;
            Changed?.Invoke();
        }

        public void Pan(float dx) => SetScroll(Scroll + dx);

        public void ZoomAt(float factor, float pivotX)
        {
            float t = X2T(pivotX);
            PixelsPerSecond = Mathf.Clamp(PixelsPerSecond * factor, MinPixelsPerSecond, MaxPixelsPerSecond);
            Scroll = Mathf.Max(0f, t * PixelsPerSecond - pivotX);
            Changed?.Invoke();
        }

        public void Fit(float length, float width)
        {
            if (width <= 60f || length <= 0f)
                return;
            PixelsPerSecond = Mathf.Clamp((width - 40f) / length, MinPixelsPerSecond, MaxPixelsPerSecond);
            Scroll = 0f;
            Changed?.Invoke();
        }
    }

    public static class FrameGrid
    {
        private static readonly int[] Steps = { 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 1200, 3000 };

        public static int LabelStep(float framePixels, float minSpacing = 50f)
        {
            foreach (var s in Steps)
                if (s * framePixels >= minSpacing)
                    return s;
            return Steps[^1];
        }

        public static int MinorStep(float framePixels, int labelStep)
        {
            if (framePixels >= 6f)
                return 1;
            foreach (var s in Steps)
                if (s * framePixels >= 6f && labelStep % s == 0)
                    return s;
            return labelStep;
        }

        public static string Format(float time, float fps)
        {
            int fpsInt = Mathf.Max(1, Mathf.RoundToInt(fps));
            int total = Mathf.Max(0, Mathf.RoundToInt(time * fps));
            return $"{total / fpsInt}:{total % fpsInt:00}";
        }
    }
}
