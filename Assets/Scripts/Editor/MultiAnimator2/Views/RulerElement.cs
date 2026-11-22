using UnityEngine;
using UnityEngine.UIElements;

namespace MultiAnimator2
{
    public sealed class RulerElement : VisualElement
    {
        private readonly MultiAnimatorContext _ctx;

        public RulerElement(MultiAnimatorContext ctx)
        {
            _ctx = ctx;
            AddToClassList("ma-ruler");
            generateVisualContent += Draw;
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<WheelEvent>(OnWheel);
        }

        private void Draw(MeshGenerationContext mgc)
        {
            var p = mgc.painter2D;
            var r = contentRect;
            var session = _ctx.Session;
            var axis = _ctx.Axis;

            Paint.Rect(p, r, Palette.Ruler);
            Paint.HLine(p, r.height - 0.5f, 0f, r.width, Palette.RowLine);

            if (session.State == null)
                return;

            float fps = session.FrameRate;
            float framePx = axis.PixelsPerSecond / fps;
            int labelStep = FrameGrid.LabelStep(framePx);
            int minorStep = FrameGrid.MinorStep(framePx, labelStep);

            int first = Mathf.Max(0, Mathf.FloorToInt(axis.X2T(0f) * fps));
            first -= first % minorStep;
            int last = Mathf.CeilToInt(axis.X2T(r.width) * fps);

            for (int f = first; f <= last; f += minorStep)
            {
                float x = axis.T2X(f / fps);
                bool major = f % labelStep == 0;
                float h = major ? r.height * 0.55f : r.height * 0.22f;
                Paint.VLine(p, x, r.height - h, r.height, Palette.RulerTick);
                if (major)
                    mgc.DrawText(f.ToString(), new Vector2(x + 3f, 2f), 10f, Palette.RulerText);
            }

            float endX = axis.T2X(session.Length);
            if (endX >= 0f && endX <= r.width)
                Paint.VLine(p, endX, 0f, r.height, Palette.EndMarker);

            float px = axis.T2X(session.Time);
            if (px >= -6f && px <= r.width + 6f)
            {
                p.fillColor = Palette.Playhead;
                p.BeginPath();
                p.MoveTo(new Vector2(px - 6f, 2f));
                p.LineTo(new Vector2(px + 6f, 2f));
                p.LineTo(new Vector2(px + 6f, r.height - 8f));
                p.LineTo(new Vector2(px, r.height - 1f));
                p.LineTo(new Vector2(px - 6f, r.height - 8f));
                p.ClosePath();
                p.Fill();
            }
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || _ctx.Session.State == null)
                return;
            _ctx.Preview.Pause();
            _ctx.Session.SetTime(_ctx.Axis.X2T(evt.localPosition.x));
            this.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!this.HasPointerCapture(evt.pointerId))
                return;
            _ctx.Session.SetTime(_ctx.Axis.X2T(evt.localPosition.x));
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!this.HasPointerCapture(evt.pointerId))
                return;
            this.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnWheel(WheelEvent evt)
        {
            if (evt.shiftKey)
                _ctx.Axis.Pan(evt.delta.y * 20f);
            else
                _ctx.Axis.ZoomAt(Mathf.Pow(1.1f, -evt.delta.y), evt.localMousePosition.x);
            evt.StopPropagation();
        }
    }
}
