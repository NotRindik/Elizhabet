// Editor/MultiAnimatorEditorWindow.Timeline.cs
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class MultiAnimatorEditorWindow
{
    // ═══════════════════════════════════════════════════
    // CONSTANTS
    // ═══════════════════════════════════════════════════

    private const float RULER_H    = 22f;
    private const float TRACK_H    = 28f;
    private const float LABEL_W    = 130f;
    private const float SPLITTER_W = 5f;
    private const float DIAMOND    = 5f;

    // ═══════════════════════════════════════════════════
    // TIMELINE STATE
    // ═══════════════════════════════════════════════════

    [NonSerialized] private float _zoom      = 200f;
    [NonSerialized] private float _scrollX   = 0f;
    [NonSerialized] private bool  _isRecording = false;

    // Splitter
    [NonSerialized] private float _sidePanelWidth  = 260f;
    [NonSerialized] private bool  _draggingSplitter = false;

    // Side panel
    [NonSerialized] private Vector2 _sidePanelScroll;
    [NonSerialized] private UnityEditor.Editor  _stateEditor;

    // Keyframe selection
    private struct KeyframeRef
    {
        public AnimationClip      Clip;
        public EditorCurveBinding Binding;
        public float              Time;   // идентификатор — по времени, не по индексу
    }
    [NonSerialized] private List<KeyframeRef> _selectedKeys = new();

    // Clipboard
    private struct ClipEntry
    {
        public EditorCurveBinding Binding;
        public Keyframe           Key;
    }
    [NonSerialized] private List<ClipEntry> _clipboard = new();

    // Box-select
    [NonSerialized] private bool    _boxSelecting = false;
    [NonSerialized] private Vector2 _boxStart;  // screen space
    [NonSerialized] private Vector2 _boxEnd;

    // Drag
    [NonSerialized] private bool  _draggingPlayhead  = false;
    [NonSerialized] private bool  _draggingKeyframes = false;
    [NonSerialized] private float _dragStartTime;

    // Cached timeline rect for cross-method use
    [NonSerialized] private Rect _lastTracksRect;

    // ═══════════════════════════════════════════════════
    // TIMELINE SECTION (вызывается из DrawMainLayout)
    // ═══════════════════════════════════════════════════

    private void DrawTimelineSection()
    {
        DrawTimelineToolbar();

        if (_selectedState == null || _selectedState.parts == null
            || _selectedState.parts.Count == 0)
        {
            GUILayout.Label("  Выбери стейт с партами →", EditorStyles.miniLabel,
                            GUILayout.Height(40f));
            return;
        }

        int   count       = _selectedState.parts.Count;
        float contentH    = RULER_H + count * TRACK_H;
        float contentW    = EditorGUIUtility.currentViewWidth - _sidePanelWidth - SPLITTER_W - 8f;

        // Резервируем место под таймлайн
        Rect area = GUILayoutUtility.GetRect(contentW, contentH);

        Rect rulerRect  = new Rect(area.x + LABEL_W, area.y, area.width - LABEL_W, RULER_H);
        Rect labelsRect = new Rect(area.x, area.y + RULER_H, LABEL_W, count * TRACK_H);
        Rect tracksRect = new Rect(area.x + LABEL_W, area.y + RULER_H,
                                    area.width - LABEL_W, count * TRACK_H);
        Rect fullRect   = new Rect(area.x + LABEL_W, area.y, area.width - LABEL_W, contentH);

        _lastTracksRect = tracksRect;

        EditorGUI.DrawRect(area, new Color(0.12f, 0.12f, 0.12f));

        DrawRuler(rulerRect);
        DrawTrackLabels(labelsRect, count);
        DrawTracksContent(tracksRect, count);
        DrawBoxSelectRect(tracksRect);
        DrawPlayhead(fullRect);

        HandleTimelineInput(area, tracksRect);

        // Статус
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"  ⏱ {_currentTime:F3}s", EditorStyles.miniLabel);
        if (_selectedKeys.Count > 0)
            GUILayout.Label($"  [{_selectedKeys.Count} ключей]", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label("Ctrl+Scroll = zoom", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        // Горизонтальный скролл
        float maxPx    = GetMaxClipLength() * _zoom + 40f;
        float newScroll = GUILayout.HorizontalScrollbar(
            _scrollX, area.width - LABEL_W, 0f, maxPx,
            GUILayout.ExpandWidth(true));
        if (!Mathf.Approximately(newScroll, _scrollX))
        {
            _scrollX = newScroll;
            Repaint();
        }
    }

    // ═══════════════════════════════════════════════════
    // TOOLBAR
    // ═══════════════════════════════════════════════════

    private void DrawTimelineToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button(_isPlaying ? "⏸" : "▶", EditorStyles.toolbarButton, GUILayout.Width(28)))
        {
            if (_isPlaying) StopPreview(); else StartPreview();
        }
        if (GUILayout.Button("■", EditorStyles.toolbarButton, GUILayout.Width(28)))
            StopAndReset();

        GUILayout.Space(4);

        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = _isRecording ? new Color(1f, 0.2f, 0.2f) : Color.white;
        if (GUILayout.Button("●", EditorStyles.toolbarButton, GUILayout.Width(28)))
            ToggleRecording();
        GUI.backgroundColor = prevBg;

        GUILayout.Space(8);
        GUILayout.Label(_currentTime.ToString("F3") + "s", EditorStyles.miniLabel, GUILayout.Width(52));
        GUILayout.FlexibleSpace();

        GUILayout.Label("Zoom", EditorStyles.miniLabel, GUILayout.Width(32));
        float nz = GUILayout.HorizontalSlider(_zoom, 40f, 900f, GUILayout.Width(80));
        if (!Mathf.Approximately(nz, _zoom)) { _zoom = nz; Repaint(); }
        GUILayout.Space(8);

        GUI.enabled = _selectedKeys.Count > 0;
        if (GUILayout.Button("Copy Keys", EditorStyles.toolbarButton, GUILayout.Width(68)))
            CopyKeyframes();
        GUI.enabled = _clipboard.Count > 0;
        if (GUILayout.Button("Paste Keys", EditorStyles.toolbarButton, GUILayout.Width(68)))
            PasteKeyframes();
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    // ═══════════════════════════════════════════════════
    // RULER
    // ═══════════════════════════════════════════════════

    private void DrawRuler(Rect rect)
    {
        EditorGUI.DrawRect(rect, new Color(0.19f, 0.19f, 0.19f));
        if (Event.current.type != EventType.Repaint) return;

        float maxT = GetMaxClipLength();
        float step = CalcStep(maxT);
        var textStyle = new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = new Color(0.62f, 0.62f, 0.62f) } };

        GUI.BeginClip(rect);
        for (float t = 0f; t <= maxT + step * 0.5f; t += step)
        {
            float x = T2X(t);
            if (x < -2f || x > rect.width + 2f) continue;
            bool major = Mathf.Round(t / step) % 2 == 0;
            EditorGUI.DrawRect(new Rect(x, rect.height - (major ? 10f : 5f), 1f, major ? 10f : 5f),
                new Color(0.52f, 0.52f, 0.52f));
            if (major)
                GUI.Label(new Rect(x + 2f, 1f, 60f, rect.height), t.ToString("F2"), textStyle);
        }
        GUI.EndClip();
    }

    // ═══════════════════════════════════════════════════
    // TRACK LABELS
    // ═══════════════════════════════════════════════════

    private void DrawTrackLabels(Rect rect, int count)
    {
        EditorGUI.DrawRect(rect, new Color(0.17f, 0.17f, 0.17f));
        var nameStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal    = { textColor = new Color(0.85f, 0.85f, 0.85f) },
            padding   = new RectOffset(8, 4, 0, 0),
            alignment = TextAnchor.MiddleLeft
        };
        var clipStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal    = { textColor = new Color(0.42f, 0.63f, 1f, 0.8f) },
            padding   = new RectOffset(4, 6, 0, 0),
            alignment = TextAnchor.MiddleRight,
            fontSize  = 9
        };

        for (int i = 0; i < count; i++)
        {
            Rect row = new Rect(rect.x, rect.y + i * TRACK_H, rect.width, TRACK_H);
            if (i % 2 == 1) EditorGUI.DrawRect(row, new Color(1, 1, 1, 0.022f));
            EditorGUI.DrawRect(new Rect(row.x, row.yMax - 1f, row.width, 1f),
                new Color(0f, 0f, 0f, 0.4f));
            var part = _selectedState.parts[i];
            GUI.Label(row, part.partName, nameStyle);
            if (part.clip != null)
                GUI.Label(row, part.clip.name, clipStyle);
        }
    }

    // ═══════════════════════════════════════════════════
    // TRACKS CONTENT
    // ═══════════════════════════════════════════════════

    private void DrawTracksContent(Rect rect, int count)
    {
        EditorGUI.DrawRect(rect, new Color(0.14f, 0.14f, 0.14f));
        GUI.BeginClip(rect);

        DrawGridLines(rect.width, rect.height);

        for (int i = 0; i < count; i++)
        {
            Rect row = new Rect(0, i * TRACK_H, rect.width, TRACK_H);
            if (i % 2 == 1) EditorGUI.DrawRect(row, new Color(1, 1, 1, 0.022f));
            EditorGUI.DrawRect(new Rect(0, row.yMax - 1f, rect.width, 1f),
                new Color(0f, 0f, 0f, 0.3f));

            var clip = _selectedState.parts[i].clip;
            if (clip == null) continue;

            // Полоска клипа
            float cw = Mathf.Max(0f, T2X(clip.length));
            if (cw > 0f)
            {
                var bar = new Rect(0, row.y + 4f, cw, row.height - 8f);
                EditorGUI.DrawRect(bar, new Color(0.22f, 0.42f, 0.65f, 0.28f));
                DrawRectOutline(bar, new Color(0.28f, 0.52f, 0.82f, 0.5f));
            }

            DrawKeyframesOnRow(row, clip, rect.width);
        }

        GUI.EndClip();
    }

    private void DrawKeyframesOnRow(Rect row, AnimationClip clip, float trackW)
    {
        var times = CollectUniqueTimes(clip);
        float cy  = row.y + row.height * 0.5f;

        foreach (float t in times)
        {
            float x = T2X(t);
            if (x < -DIAMOND - 1f || x > trackW + DIAMOND + 1f) continue;

            bool sel = _selectedKeys.Exists(
                k => ReferenceEquals(k.Clip, clip) && Mathf.Abs(k.Time - t) < 0.001f);
            DrawDiamond(new Vector2(x, cy), sel);
        }
    }

    private HashSet<float> CollectUniqueTimes(AnimationClip clip)
    {
        var set = new HashSet<float>();
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            var curve = AnimationUtility.GetEditorCurve(clip, b);
            if (curve == null) continue;
            foreach (var k in curve.keys)
                set.Add(Mathf.Round(k.time * 1000f) / 1000f);
        }
        return set;
    }

    // ═══════════════════════════════════════════════════
    // BOX SELECT RECT (overlay поверх треков)
    // ═══════════════════════════════════════════════════

    private void DrawBoxSelectRect(Rect tracksRect)
    {
        if (!_boxSelecting) return;
        if (Event.current.type != EventType.Repaint) return;

        // Конвертируем screen-coords в tracksRect-local
        Vector2 a = _boxStart - new Vector2(tracksRect.x, tracksRect.y);
        Vector2 b = _boxEnd   - new Vector2(tracksRect.x, tracksRect.y);

        Rect box = new Rect(
            Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
            Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));

        GUI.BeginClip(tracksRect);
        EditorGUI.DrawRect(box, new Color(0.3f, 0.6f, 1f, 0.12f));
        DrawRectOutline(box, new Color(0.4f, 0.7f, 1f, 0.7f));
        GUI.EndClip();
    }

    // ═══════════════════════════════════════════════════
    // PLAYHEAD
    // ═══════════════════════════════════════════════════

    private void DrawPlayhead(Rect rect)
    {
        if (Event.current.type != EventType.Repaint) return;
        GUI.BeginClip(rect);
        float x = T2X(_currentTime);
        EditorGUI.DrawRect(new Rect(x, RULER_H, 1f, rect.height - RULER_H),
            new Color(1f, 0.35f, 0.35f, 0.88f));
        // Треугольник-хэндл
        EditorGUI.DrawRect(new Rect(x - 5f, 0f, 11f, 10f),  new Color(1f, 0.28f, 0.28f));
        EditorGUI.DrawRect(new Rect(x - 3f, 8f,  7f,  7f),  new Color(1f, 0.28f, 0.28f));
        GUI.EndClip();
    }

    // ═══════════════════════════════════════════════════
    // INPUT
    // ═══════════════════════════════════════════════════

    private void HandleTimelineInput(Rect area, Rect tracksRect)
    {
        Event e = Event.current;

        // Ctrl+Scroll = zoom
        if (e.type == EventType.ScrollWheel && e.control && area.Contains(e.mousePosition))
        {
            _zoom = Mathf.Clamp(_zoom - e.delta.y * 12f, 40f, 900f);
            e.Use(); Repaint(); return;
        }

        // Ruler: клик/drag по линейке = плейхед
        Rect rulerZone = new Rect(area.x + LABEL_W, area.y, area.width - LABEL_W, RULER_H);
        if (rulerZone.Contains(e.mousePosition))
        {
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                _draggingPlayhead = true;
                SetTime(X2T(e.mousePosition.x - rulerZone.x));
                e.Use();
            }
            if (_draggingPlayhead && e.type == EventType.MouseDrag && e.button == 0)
            {
                SetTime(Mathf.Clamp(X2T(e.mousePosition.x - rulerZone.x), 0f, GetMaxClipLength()));
                e.Use();
            }
            if (e.type == EventType.MouseUp) _draggingPlayhead = false;
            return;
        }

        // Tracks: box-select / keyframe drag
        if (!tracksRect.Contains(e.mousePosition) && e.type == EventType.MouseDown)
            return;

        float localX = e.mousePosition.x - tracksRect.x;
        float t      = Mathf.Clamp(X2T(localX), 0f, GetMaxClipLength());

        switch (e.type)
        {
            case EventType.MouseDown when e.button == 0:
                if (TrySelectKeyframe(e.mousePosition, tracksRect, t))
                {
                    _draggingKeyframes = true;
                    _dragStartTime     = t;
                }
                else
                {
                    // Начинаем box-select
                    if (!e.shift) _selectedKeys.Clear();
                    _boxSelecting = true;
                    _boxStart     = e.mousePosition;
                    _boxEnd       = e.mousePosition;
                }
                e.Use(); break;

            case EventType.MouseDrag when e.button == 0:
                if (_draggingKeyframes && _selectedKeys.Count > 0)
                {
                    float delta = t - _dragStartTime;
                    _dragStartTime = t;
                    MoveSelectedKeyframes(delta);
                }
                else if (_boxSelecting)
                {
                    _boxEnd = e.mousePosition;
                    Repaint();
                }
                e.Use(); break;

            case EventType.MouseUp:
                if (_boxSelecting)
                    FinishBoxSelect(tracksRect);
                _boxSelecting      = false;
                _draggingPlayhead  = false;
                _draggingKeyframes = false;
                Repaint(); break;
        }
    }

    private bool TrySelectKeyframe(Vector2 mousePos, Rect tracksRect, float t)
    {
        if (_selectedState == null) return false;

        float localY = mousePos.y - tracksRect.y;
        int   idx    = Mathf.FloorToInt(localY / TRACK_H);
        if (idx < 0 || idx >= _selectedState.parts.Count) return false;

        var part = _selectedState.parts[idx];
        if (part.clip == null) return false;

        float localX = mousePos.x - tracksRect.x;

        foreach (var binding in AnimationUtility.GetCurveBindings(part.clip))
        {
            var curve = AnimationUtility.GetEditorCurve(part.clip, binding);
            if (curve == null) continue;

            foreach (var key in curve.keys)
            {
                if (Mathf.Abs(T2X(key.time) - localX) > 8f) continue;

                if (!Event.current.shift) _selectedKeys.Clear();
                _selectedKeys.Add(new KeyframeRef
                {
                    Clip    = part.clip,
                    Binding = binding,
                    Time    = key.time
                });
                Repaint();
                return true;
            }
        }
        return false;
    }

    private void FinishBoxSelect(Rect tracksRect)
    {
        if (_selectedState == null) return;

        Vector2 a = new Vector2(
            Mathf.Min(_boxStart.x, _boxEnd.x),
            Mathf.Min(_boxStart.y, _boxEnd.y));
        Vector2 b = new Vector2(
            Mathf.Max(_boxStart.x, _boxEnd.x),
            Mathf.Max(_boxStart.y, _boxEnd.y));

        // Конвертируем в tracksRect-local
        a -= new Vector2(tracksRect.x, tracksRect.y);
        b -= new Vector2(tracksRect.x, tracksRect.y);

        float tMin = X2T(a.x), tMax = X2T(b.x);
        int   rowMin = Mathf.FloorToInt(a.y / TRACK_H);
        int   rowMax = Mathf.FloorToInt(b.y / TRACK_H);

        for (int i = Mathf.Max(0, rowMin);
             i <= Mathf.Min(rowMax, _selectedState.parts.Count - 1); i++)
        {
            var clip = _selectedState.parts[i].clip;
            if (clip == null) continue;

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null) continue;

                foreach (var key in curve.keys)
                {
                    if (key.time < tMin || key.time > tMax) continue;
                    // Не дублируем
                    if (_selectedKeys.Exists(k => ReferenceEquals(k.Clip, clip)
                                               && k.Binding.Equals(binding)
                                               && Mathf.Abs(k.Time - key.time) < 0.001f))
                        continue;

                    _selectedKeys.Add(new KeyframeRef
                        { Clip = clip, Binding = binding, Time = key.time });
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // KEYFRAME OPERATIONS (исправленный move)
    // ═══════════════════════════════════════════════════

    private void MoveSelectedKeyframes(float deltaSeconds)
    {
        if (Mathf.Approximately(deltaSeconds, 0f)) return;

        // Группируем по (clip, binding) — обрабатываем каждую кривую один раз
        var groups = new Dictionary<(AnimationClip, EditorCurveBinding), List<float>>();
        foreach (var kref in _selectedKeys)
        {
            var key = (kref.Clip, kref.Binding);
            if (!groups.ContainsKey(key)) groups[key] = new List<float>();
            groups[key].Add(kref.Time);
        }

        foreach (var (pair, times) in groups)
        {
            var curve = AnimationUtility.GetEditorCurve(pair.Item1, pair.Item2);
            if (curve == null) continue;

            Undo.RecordObject(pair.Item1, "Move Keyframes");

            // Собираем данные ключей которые двигаем
            var toMove = new List<(float oldTime, Keyframe key)>();
            foreach (float time in times)
            {
                int idx = FindKeyByTime(curve, time);
                if (idx < 0) continue;
                toMove.Add((time, curve.keys[idx]));
            }

            // Удаляем в обратном порядке (чтобы индексы не сдвигались)
            toMove.Sort((a, b) => {
                int ia = FindKeyByTime(curve, a.oldTime);
                int ib = FindKeyByTime(curve, b.oldTime);
                return ib.CompareTo(ia);
            });
            foreach (var (oldTime, _) in toMove)
            {
                int idx = FindKeyByTime(curve, oldTime);
                if (idx >= 0) curve.RemoveKey(idx);
            }

            // Добавляем на новые позиции
            foreach (var (_, k) in toMove)
            {
                var newKey  = k;
                newKey.time = Mathf.Max(0f, k.time + deltaSeconds);
                curve.AddKey(newKey);
            }

            AnimationUtility.SetEditorCurve(pair.Item1, pair.Item2, curve);
            EditorUtility.SetDirty(pair.Item1);
        }

        // Обновляем времена в _selectedKeys
        for (int i = 0; i < _selectedKeys.Count; i++)
        {
            var k = _selectedKeys[i];
            k.Time = Mathf.Max(0f, k.Time + deltaSeconds);
            _selectedKeys[i] = k;
        }

        Repaint();
    }

    private int FindKeyByTime(AnimationCurve curve, float time, float tol = 0.001f)
    {
        for (int i = 0; i < curve.keys.Length; i++)
            if (Mathf.Abs(curve.keys[i].time - time) < tol) return i;
        return -1;
    }

    private void CopyKeyframes()
    {
        _clipboard.Clear();
        foreach (var kref in _selectedKeys)
        {
            var curve = AnimationUtility.GetEditorCurve(kref.Clip, kref.Binding);
            if (curve == null) continue;
            int idx = FindKeyByTime(curve, kref.Time);
            if (idx < 0) continue;
            _clipboard.Add(new ClipEntry { Binding = kref.Binding, Key = curve.keys[idx] });
        }
    }

    private void PasteKeyframes()
    {
        if (_clipboard.Count == 0) return;

        var target = _selectedKeys.Count > 0
            ? _selectedKeys[0].Clip
            : _selectedState?.parts[0]?.clip;
        if (target == null) return;

        Undo.RecordObject(target, "Paste Keyframes");
        float origin = _clipboard[0].Key.time;

        foreach (var entry in _clipboard)
        {
            var k  = entry.Key;
            k.time = _currentTime + (entry.Key.time - origin);
            var curve = AnimationUtility.GetEditorCurve(target, entry.Binding)
                        ?? new AnimationCurve();
            curve.AddKey(k);
            AnimationUtility.SetEditorCurve(target, entry.Binding, curve);
        }

        EditorUtility.SetDirty(target);
        Repaint();
    }

    // ═══════════════════════════════════════════════════
    // RECORDING
    // ═══════════════════════════════════════════════════

    private void ToggleRecording()
    {
        _isRecording = !_isRecording;
        if (_isRecording)
        {
            if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
            Undo.postprocessModifications += OnPostprocessMods;
        }
        else
        {
            Undo.postprocessModifications -= OnPostprocessMods;
        }
        OnRecordChanged?.Invoke(_isRecording);
        Repaint();
    }

    private UndoPropertyModification[] OnPostprocessMods(UndoPropertyModification[] mods)
    {
        if (!_isRecording || _selectedState == null) return mods;

        foreach (var mod in mods)
        {
            if (!(mod.currentValue.target is Component comp)) continue;

            foreach (var part in _selectedState.parts)
            {
                if (part.clip == null) continue;
                if (!_animatorByPart.TryGetValue(part.partName, out var animator)) continue;
                if (!comp.transform.IsChildOf(animator.transform)
                    && comp.transform != animator.transform) continue;

                if (!float.TryParse(mod.currentValue.value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float val)) break;

                var binding = new EditorCurveBinding
                {
                    path         = AnimationUtility.CalculateTransformPath(
                                       comp.transform, animator.transform),
                    type         = comp.GetType(),
                    propertyName = mod.currentValue.propertyPath
                };

                Undo.RecordObject(part.clip, "Record Keyframe");
                var curve = AnimationUtility.GetEditorCurve(part.clip, binding)
                            ?? new AnimationCurve();
                curve.AddKey(new Keyframe(_currentTime, val));
                AnimationUtility.SetEditorCurve(part.clip, binding, curve);
                EditorUtility.SetDirty(part.clip);
                break;
            }
        }

        Repaint();
        return mods;
    }

    // ═══════════════════════════════════════════════════
    // SPLITTER
    // ═══════════════════════════════════════════════════

    private void DrawSplitterHandle()
    {
        Rect r = GUILayoutUtility.GetRect(SPLITTER_W, 1f,
            GUILayout.Width(SPLITTER_W), GUILayout.ExpandHeight(true));

        EditorGUI.DrawRect(r, new Color(0.08f, 0.08f, 0.08f));
        EditorGUIUtility.AddCursorRect(r, MouseCursor.ResizeHorizontal);

        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
        {
            _draggingSplitter = true; e.Use();
        }
        if (_draggingSplitter)
        {
            if (e.type == EventType.MouseDrag)
            {
                _sidePanelWidth = Mathf.Clamp(_sidePanelWidth - e.delta.x, 150f, 520f);
                Repaint(); e.Use();
            }
            if (e.type == EventType.MouseUp) { _draggingSplitter = false; }
        }
    }

    // ═══════════════════════════════════════════════════
    // SIDE PANEL
    // ═══════════════════════════════════════════════════

    private void DrawSidePanel()
    {
        if (_activeConfig == null) return;

        // Список стейтов
        EditorGUILayout.LabelField("Стейты", EditorStyles.boldLabel);

        var states = _activeConfig.states;
        for (int i = 0; i < states.Count; i++)
        {
            var s = states[i];
            if (s == null) continue;

            bool sel = i == _selectedIndex;
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = sel ? new Color(0.38f, 0.78f, 1f) : Color.white;

            if (GUILayout.Button(s.stateName, GUILayout.Height(24)))
            {
                _selectedIndex = i;
                _selectedState = s;
                _stateEditor   = null;
                OnStateSelected?.Invoke(s);
            }
            GUI.backgroundColor = prevBg;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Добавить", EditorStyles.miniButton))
            AddState();
        if (_selectedState != null)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.38f, 0.38f);
            if (GUILayout.Button("− Удалить", EditorStyles.miniButton))
                RemoveSelectedState();
            GUI.backgroundColor = prev;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // Редактор выбранного стейта через стандартный Editor
        // (Odin перехватывает Editor.CreateEditor → рисует с [TableList], [ValueDropdown] итд)
        if (_selectedState != null)
        {
            if (_stateEditor == null || _stateEditor.target != _selectedState)
            {
                if (_stateEditor != null) DestroyImmediate(_stateEditor);
                _stateEditor = UnityEditor.Editor.CreateEditor(_selectedState);
            }

            _sidePanelScroll = EditorGUILayout.BeginScrollView(_sidePanelScroll);
            _stateEditor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }
    }

    // ═══════════════════════════════════════════════════
    // UTILS
    // ═══════════════════════════════════════════════════

    private float T2X(float t)    => t * _zoom - _scrollX;
    private float X2T(float x)    => (x + _scrollX) / _zoom;

    private float CalcStep(float maxT)
    {
        float raw  = maxT / 10f;
        if (raw <= 0f) return 0.1f;
        float step = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(raw)));
        if (raw / step > 5f) step *= 5f;
        else if (raw / step > 2f) step *= 2f;
        return step;
    }

    private void DrawGridLines(float w, float h)
    {
        float maxT = GetMaxClipLength();
        float step = CalcStep(maxT);
        var   col  = new Color(1f, 1f, 1f, 0.048f);
        for (float t = 0f; t <= maxT; t += step)
        {
            float x = T2X(t);
            if (x >= 0 && x <= w)
                EditorGUI.DrawRect(new Rect(x, 0, 1f, h), col);
        }
    }

    private void DrawDiamond(Vector2 c, bool sel)
    {
        float s   = sel ? DIAMOND + 1.5f : DIAMOND;
        Color col = sel ? new Color(1f, 0.82f, 0.15f) : new Color(0.76f, 0.76f, 0.76f);
        EditorGUI.DrawRect(new Rect(c.x - 1f, c.y - s,  2f, s), col);
        EditorGUI.DrawRect(new Rect(c.x - 1f, c.y,      2f, s), col);
        EditorGUI.DrawRect(new Rect(c.x - s,  c.y - 1f, s,  2f), col);
        EditorGUI.DrawRect(new Rect(c.x,       c.y - 1f, s,  2f), col);
    }

    private void DrawRectOutline(Rect r, Color col)
    {
        EditorGUI.DrawRect(new Rect(r.x,     r.y,     r.width, 1f), col);
        EditorGUI.DrawRect(new Rect(r.x,     r.yMax,  r.width, 1f), col);
        EditorGUI.DrawRect(new Rect(r.x,     r.y,     1f, r.height), col);
        EditorGUI.DrawRect(new Rect(r.xMax,  r.y,     1f, r.height), col);
    }
}