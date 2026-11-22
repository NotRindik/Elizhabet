using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MultiAnimator2
{
    public sealed class LanesCanvas : VisualElement
    {
        private const float KeySize = 5f;
        private const float HitRadius = 7f;

        private enum DragMode { None, Keys, Box, Pan }

        private readonly MultiAnimatorContext _ctx;
        private List<TrackRow> _rows = new();

        private DragMode _mode;
        private Vector2 _boxStart;
        private Vector2 _boxEnd;
        private HashSet<KeyRef> _boxBase;
        private float _dragStartTime;
        private float _appliedDelta;
        private float _minDragTime;
        private List<KeyRef> _dragKeys;
        private Dictionary<CurveId, CurveSnapshot> _dragOriginals;
        private int _undoGroup = -1;

        public LanesCanvas(MultiAnimatorContext ctx)
        {
            _ctx = ctx;
            AddToClassList("ma-lanes");
            generateVisualContent += Draw;
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(_ => FinishDrag());
            RegisterCallback<WheelEvent>(OnWheel);
        }

        public void SetRows(List<TrackRow> rows)
        {
            _rows = rows;
            style.height = rows.Count > 0 ? rows[^1].Bottom : 0f;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext mgc)
        {
            var p = mgc.painter2D;
            var r = contentRect;
            var session = _ctx.Session;
            var axis = _ctx.Axis;

            Paint.Rect(p, r, Palette.Background);
            if (session.State == null)
                return;

            foreach (var row in _rows)
            {
                var color = row.IsProperty ? Palette.PropertyRow : row.Part % 2 == 0 ? Palette.RowEven : Palette.RowOdd;
                Paint.Rect(p, new Rect(0f, row.Y, r.width, row.Height), color);
                if (row.Part == session.FocusedPart)
                    Paint.Rect(p, new Rect(0f, row.Y, r.width, row.Height), Palette.FocusedRow);
                Paint.HLine(p, row.Bottom - 0.5f, 0f, r.width, Palette.RowLine);
            }

            DrawGrid(p, r);

            float endX = axis.T2X(session.Length);
            if (endX < r.width)
                Paint.Rect(p, new Rect(Mathf.Max(0f, endX), 0f, r.width - Mathf.Max(0f, endX), r.height),
                    Palette.OutsideClip);

            var selectedClipTimes = new HashSet<(int, int)>();
            var selectedCurveTimes = new HashSet<(CurveId, int)>();
            foreach (var k in session.Keys)
            {
                selectedClipTimes.Add((k.Clip != null ? k.Clip.GetInstanceID() : 0, k.TimeKey));
                selectedCurveTimes.Add((k.Curve, k.TimeKey));
            }

            foreach (var row in _rows)
            {
                var clip = session.Parts[row.Part].clip;
                if (clip == null)
                    continue;

                var partColor = Palette.ForPart(row.Part);

                if (!row.IsProperty)
                {
                    float x0 = axis.T2X(0f);
                    float x1 = axis.T2X(clip.length);
                    var block = new Rect(x0, row.Y + 4f, Mathf.Max(2f, x1 - x0), row.Height - 8f);
                    Paint.Rect(p, block, Palette.WithAlpha(partColor, 0.16f));
                    Paint.Outline(p, block, Palette.WithAlpha(partColor, 0.45f));
                }

                DrawRowKeys(p, r, row, clip, partColor, selectedClipTimes, selectedCurveTimes);
            }

            if (_mode == DragMode.Box)
            {
                var box = BoxRect();
                Paint.Rect(p, box, Palette.BoxFill);
                Paint.Outline(p, box, Palette.BoxStroke);
            }
        }

        private void DrawGrid(Painter2D p, Rect r)
        {
            var session = _ctx.Session;
            var axis = _ctx.Axis;
            float fps = session.FrameRate;
            float framePx = axis.PixelsPerSecond / fps;
            int labelStep = FrameGrid.LabelStep(framePx);
            int minorStep = FrameGrid.MinorStep(framePx, labelStep);
            bool drawMinor = framePx * minorStep >= 10f;

            int first = Mathf.Max(0, Mathf.FloorToInt(axis.X2T(0f) * fps));
            first -= first % minorStep;
            int last = Mathf.CeilToInt(axis.X2T(r.width) * fps);

            for (int f = first; f <= last; f += minorStep)
            {
                bool major = f % labelStep == 0;
                if (!major && !drawMinor)
                    continue;
                Paint.VLine(p, axis.T2X(f / fps), 0f, r.height, major ? Palette.GridMajor : Palette.GridMinor);
            }
        }

        private void DrawRowKeys(Painter2D p, Rect r, TrackRow row, AnimationClip clip, Color color,
            HashSet<(int, int)> selectedClipTimes, HashSet<(CurveId, int)> selectedCurveTimes)
        {
            var info = ClipCache.Get(clip);
            float cy = row.Y + row.Height * 0.5f;
            int clipId = clip.GetInstanceID();
            var axis = _ctx.Axis;

            if (!row.IsProperty)
            {
                foreach (var t in info.Times)
                {
                    float x = axis.T2X(t);
                    if (x < -KeySize || x > r.width + KeySize)
                        continue;
                    int tk = TimeKeys.Of(t);
                    bool selected = selectedClipTimes.Contains((clipId, tk));
                    DrawKey(p, new Vector2(x, cy), info.ObjectTimes.Contains(tk), selected, color);
                    if (info.GroupsPerTime.TryGetValue(tk, out var n) && n > 1 && !_ctx.Session.Expanded.Contains(row.Part))
                        Paint.Rect(p, new Rect(x + 3f, cy - KeySize - 4f, 3f, 3f), Palette.Stacked);
                }
                return;
            }

            var g = row.Group;
            foreach (var t in g.Times)
            {
                float x = axis.T2X(t);
                if (x < -KeySize || x > r.width + KeySize)
                    continue;
                int tk = TimeKeys.Of(t);
                bool selected = false;
                foreach (var b in g.Bindings)
                {
                    if (!selectedCurveTimes.Contains((new CurveId(clip, b, g.IsObject), tk)))
                        continue;
                    selected = true;
                    break;
                }
                DrawKey(p, new Vector2(x, cy), g.IsObject, selected, color);
            }
        }

        private static void DrawKey(Painter2D p, Vector2 c, bool isObject, bool selected, Color color)
        {
            float size = selected ? KeySize + 1.5f : KeySize;
            var fill = selected ? Palette.Selected : color;
            if (isObject)
                Paint.Square(p, c, size - 1f, fill);
            else
                Paint.Diamond(p, c, size, fill);
        }

        private Rect BoxRect() => new(
            Mathf.Min(_boxStart.x, _boxEnd.x), Mathf.Min(_boxStart.y, _boxEnd.y),
            Mathf.Abs(_boxEnd.x - _boxStart.x), Mathf.Abs(_boxEnd.y - _boxStart.y));

        private IEnumerable<PropertyGroup> GroupsOf(TrackRow row, ClipInfo info) =>
            row.IsProperty ? new[] { row.Group } : info.Groups;

        private bool HitKey(Vector2 pos, out TrackRow row, out float time)
        {
            time = 0f;
            row = TrackLayout.RowAt(_rows, pos.y);
            if (row == null)
                return false;

            var clip = _ctx.Session.Parts[row.Part].clip;
            var info = ClipCache.Get(clip);
            if (info == null)
                return false;

            var times = row.IsProperty ? row.Group.Times : info.Times;
            float best = HitRadius;
            bool found = false;
            foreach (var t in times)
            {
                float d = Mathf.Abs(_ctx.Axis.T2X(t) - pos.x);
                if (d > best)
                    continue;
                best = d;
                time = t;
                found = true;
            }
            return found;
        }

        private List<KeyRef> KeysAt(TrackRow row, float time)
        {
            var clip = _ctx.Session.Parts[row.Part].clip;
            var result = new List<KeyRef>();
            var info = ClipCache.Get(clip);
            if (info == null)
                return result;
            foreach (var g in GroupsOf(row, info))
                result.AddRange(KeyOps.KeysAt(clip, g, time));
            return result;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            var session = _ctx.Session;
            if (session.State == null)
                return;

            Vector2 pos = evt.localPosition;

            if (evt.button == 2 || (evt.button == 0 && evt.altKey))
            {
                _mode = DragMode.Pan;
                this.CapturePointer(evt.pointerId);
                evt.StopPropagation();
                return;
            }

            var row = TrackLayout.RowAt(_rows, pos.y);
            if (row != null)
                session.FocusPart(row.Part);

            if (evt.button == 1)
            {
                ShowContextMenu(pos, row);
                evt.StopPropagation();
                return;
            }

            if (evt.button != 0)
                return;

            if (HitKey(pos, out var keyRow, out var keyTime))
            {
                var keys = KeysAt(keyRow, keyTime);
                bool selected = keys.Exists(session.Keys.Contains);

                if (evt.actionKey && selected)
                {
                    foreach (var k in keys)
                        session.Keys.Remove(k);
                    session.Notify(SessionChange.Selection);
                    evt.StopPropagation();
                    return;
                }

                if (!selected)
                {
                    if (!evt.shiftKey && !evt.actionKey)
                        session.Keys.Clear();
                    session.Keys.AddRange(keys);
                }

                BeginKeyDrag(keyTime);
            }
            else if (evt.clickCount == 2)
            {
                session.SetTime(_ctx.Axis.X2T(pos.x));
                evt.StopPropagation();
                return;
            }
            else
            {
                if (!evt.shiftKey && !evt.actionKey)
                    session.Keys.Clear();
                _boxBase = new HashSet<KeyRef>(session.Keys);
                _boxStart = _boxEnd = pos;
                _mode = DragMode.Box;
            }

            session.Notify(SessionChange.Selection);
            this.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void BeginKeyDrag(float time)
        {
            _mode = DragMode.Keys;
            _dragKeys = _ctx.Session.Keys.ToList();
            _dragOriginals = null;
            _dragStartTime = time;
            _appliedDelta = 0f;
            _minDragTime = float.MaxValue;
            foreach (var k in _dragKeys)
                _minDragTime = Mathf.Min(_minDragTime, k.Time);
            _undoGroup = -1;
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!this.HasPointerCapture(evt.pointerId))
                return;

            Vector2 pos = evt.localPosition;

            switch (_mode)
            {
                case DragMode.Pan:
                    _ctx.Axis.Pan(-evt.deltaPosition.x);
                    break;

                case DragMode.Keys:
                    DragKeys(pos);
                    break;

                case DragMode.Box:
                    _boxEnd = pos;
                    UpdateBoxSelection();
                    break;
            }

            evt.StopPropagation();
        }

        private void DragKeys(Vector2 pos)
        {
            var session = _ctx.Session;
            float delta = session.Snap(_ctx.Axis.X2T(pos.x) - _dragStartTime);
            delta = Mathf.Max(delta, -_minDragTime);
            if (Mathf.Approximately(delta, _appliedDelta))
                return;

            if (_dragOriginals == null)
            {
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Move Keys");
                _undoGroup = Undo.GetCurrentGroup();
                _dragOriginals = KeyOps.Snapshot(_dragKeys);
            }

            _appliedDelta = delta;
            var moved = KeyOps.ApplyMove(_dragOriginals, _dragKeys, delta, "Move Keys");
            session.Keys.Set(moved);
            session.Notify(SessionChange.Data | SessionChange.Selection);
        }

        private void UpdateBoxSelection()
        {
            var session = _ctx.Session;
            var box = BoxRect();
            float tMin = _ctx.Axis.X2T(box.xMin);
            float tMax = _ctx.Axis.X2T(box.xMax);
            var result = new HashSet<KeyRef>(_boxBase);

            foreach (var row in _rows)
            {
                if (row.Bottom < box.yMin || row.Y > box.yMax)
                    continue;
                var clip = session.Parts[row.Part].clip;
                var info = ClipCache.Get(clip);
                if (info == null)
                    continue;

                foreach (var g in GroupsOf(row, info))
                    foreach (var t in g.Times)
                        if (t >= tMin && t <= tMax)
                            result.UnionWith(KeyOps.KeysAt(clip, g, t));
            }

            session.Keys.Set(result);
            session.Notify(SessionChange.Selection);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!this.HasPointerCapture(evt.pointerId))
                return;
            this.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void FinishDrag()
        {
            if (_mode == DragMode.Keys && _undoGroup >= 0)
                Undo.CollapseUndoOperations(_undoGroup);

            _mode = DragMode.None;
            _dragKeys = null;
            _dragOriginals = null;
            _boxBase = null;
            _undoGroup = -1;
            MarkDirtyRepaint();
        }

        private void OnWheel(WheelEvent evt)
        {
            if (evt.actionKey)
            {
                _ctx.Axis.ZoomAt(Mathf.Pow(1.1f, -evt.delta.y), evt.localMousePosition.x);
                evt.StopPropagation();
            }
            else if (evt.shiftKey)
            {
                _ctx.Axis.Pan(evt.delta.y * 20f);
                evt.StopPropagation();
            }
        }

        private void ShowContextMenu(Vector2 pos, TrackRow row)
        {
            var session = _ctx.Session;
            var menu = new GenericMenu();
            float time = session.Snap(_ctx.Axis.X2T(pos.x));
            var clip = row != null ? session.Parts[row.Part].clip : null;

            if (HitKey(pos, out var keyRow, out var keyTime))
            {
                var keys = KeysAt(keyRow, keyTime);
                if (!keys.Exists(session.Keys.Contains))
                {
                    session.Keys.Set(keys);
                    session.Notify(SessionChange.Selection);
                }
            }

            if (session.Keys.Count > 0)
            {
                menu.AddItem(new GUIContent("Копировать ключи"), false, _ctx.CopySelectedKeys);
                menu.AddItem(new GUIContent("Удалить ключи"), false, _ctx.DeleteSelectedKeys);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Копировать ключи"));
                menu.AddDisabledItem(new GUIContent("Удалить ключи"));
            }

            if (clip != null && _ctx.Clipboard.Count > 0)
                menu.AddItem(new GUIContent("Вставить сюда"), false, () => _ctx.PasteAt(time, clip));
            else
                menu.AddDisabledItem(new GUIContent("Вставить сюда"));

            if (clip != null)
            {
                var groups = row.IsProperty ? new List<PropertyGroup> { row.Group } : ClipCache.Get(clip).Groups;
                menu.AddItem(new GUIContent("Добавить ключ здесь"), false, () =>
                {
                    var added = KeyOps.AddKeys(clip, groups, time);
                    session.Keys.Set(added);
                    session.Notify(SessionChange.Data | SessionChange.Selection);
                });
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Перейти сюда"), false, () => session.SetTime(time));
            menu.ShowAsContext();
        }
    }
}
