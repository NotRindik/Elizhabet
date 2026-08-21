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
    private const float LABEL_W    = 170f;
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

    // Side panel
    [NonSerialized] private Vector2 _sidePanelScroll;
    [NonSerialized] private UnityEditor.Editor  _stateEditor;

    // Keyframe selection
    private struct KeyframeRef
    {
        public AnimationClip      Clip;
        public EditorCurveBinding Binding;
        public float              Time;              // идентификатор — по времени, не по индексу
        public bool               IsObjectReference; // true = ключ смены спрайта/материала/объекта (ObjectReferenceKeyframe),
                                                       // false = обычная float-кривая (position, rotation, blend shape и т.д.)
    }
    [NonSerialized] private List<KeyframeRef> _selectedKeys = new();

    // Clipboard
    private struct ClipEntry
    {
        public EditorCurveBinding  Binding;
        public bool                IsObjectReference;
        public Keyframe            Key;         // валидно, если !IsObjectReference
        public UnityEngine.Object  ObjectValue; // валидно, если IsObjectReference
        public float               Time;        // общее поле времени — чтобы не лезть то в Key.time, то в отдельное поле
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
    // TRACK EXPAND/COLLAPSE (настройки клипа под стрелочкой)
    // ═══════════════════════════════════════════════════

    // Раньше стрелочка "▸" была чисто декоративной. Индексы партов, у которых
    // она сейчас "раскрыта" — под такой строкой рисуется мини-панель настроек клипа
    // и список анимируемых свойств, каждое со своей лентой ключей.
    [NonSerialized] private HashSet<int> _expandedTracks = new();

    // Раскрытая область теперь состоит из двух частей:
    // 1) блок настроек клипа (FPS, Loop Time, Loop Pose) — фиксированной высоты;
    // 2) список анимируемых свойств — по одной строке на каждое (см. PropertyGroup),
    //    высота зависит от количества свойств в конкретном клипе.
    // Раньше это был один фиксированный EXPANDED_EXTRA_H, из-за чего все ключи
    // на строке рисовались слитно в одну общую ленту — если два разных свойства
    // (например позиция и смена спрайта)키лись в один и тот же момент времени,
    // их ромбики/квадратики просто накладывались друг на друга.
    private const float SETTINGS_BLOCK_H    = 60f;  // FPS + Loop Time + Loop Pose
    private const float PROPERTIES_HEADER_H = 16f;  // заголовок "Свойства (N):"
    private const float PROPERTY_ROW_H      = 18f;  // одна строка = одно анимируемое свойство

    // Кэш накопленных Y-смещений строк за этот кадр (см. ComputeRowTops) — раньше
    // все строки были одной фиксированной высоты TRACK_H и индекс строки под
    // курсором считался как FloorToInt(y / TRACK_H). Теперь высота строки
    // переменная (раскрытые треки выше обычных, и по-разному — зависит от числа
    // свойств в клипе), поэтому позиции нужно копить кумулятивно и переиспользовать
    // в HandleTimelineInput/TrySelectKeyframe/FinishBoxSelect, которые выполняются
    // уже после того, как разметка посчитана.
    [NonSerialized] private float[] _lastRowTops;

    private float RowHeight(int index)
    {
        if (!_expandedTracks.Contains(index)) return TRACK_H;

        AnimationClip clip = null;
        if (_selectedState?.parts != null && index < _selectedState.parts.Count)
            clip = _selectedState.parts[index].clip;

        return TRACK_H + ExpandedContentHeight(clip);
    }

    private float ExpandedContentHeight(AnimationClip clip)
    {
        if (clip == null) return SETTINGS_BLOCK_H; // просто "клип не назначен" в блоке настроек
        int groupCount = CollectPropertyGroups(clip).Count;
        return SETTINGS_BLOCK_H + PROPERTIES_HEADER_H + groupCount * PROPERTY_ROW_H;
    }

    private float[] ComputeRowTops(int count)
    {
        var tops = new float[count + 1];
        float y = 0f;
        for (int i = 0; i < count; i++)
        {
            tops[i] = y;
            y += RowHeight(i);
        }
        tops[count] = y;
        return tops;
    }

    // Индекс строки по локальной Y-координате внутри tracksRect/labelsRect,
    // с учётом переменной высоты строк (см. _lastRowTops).
    private int RowIndexAtLocalY(float localY, int count)
    {
        if (_lastRowTops == null) return -1;
        for (int i = 0; i < count; i++)
            if (localY >= _lastRowTops[i] && localY < _lastRowTops[i + 1]) return i;
        return -1;
    }

    // ═══════════════════════════════════════════════════
    // PROPERTY GROUPS — разбивка ключей клипа по анимируемым свойствам
    // ═══════════════════════════════════════════════════
    //
    // Раньше на таймлайне у каждого парта была одна общая лента ключей на весь
    // клип — если, скажем, Position.x/y/z и смена спрайта (m_Sprite) кеились
    // в одну и ту же секунду, все их ромбики/квадратики рисовались друг на
    // друге в одной точке и были неотличимы. PropertyGroup группирует биндинги
    // клипа по тому, ЧТО именно они меняют (объект/компонент + свойство,
    // без разбивки на x/y/z), чтобы при раскрытии трека каждое свойство можно
    // было увидеть и подёргать на своей отдельной строке.

    private struct PropertyGroup
    {
        public string DisplayName;
        public string Path;   // полный путь биндинга — только для сортировки (родитель раньше потомков)
        public int    Depth;  // глубина в иерархии — на сколько отступить строку при отрисовке
        public bool   IsObjectReference;
        public List<EditorCurveBinding> FloatBindings; // x/y/z одного свойства объединены в одну группу
        public EditorCurveBinding ObjectBinding;        // валиден только для object-reference группы
    }

    // "m_LocalPosition.x" → "m_LocalPosition" — x/y/z (и .w у кватернионов)
    // одного свойства должны попасть в одну группу, а не рисоваться тремя
    // отдельными строками.
    private static string StripAxisSuffix(string propertyName)
    {
        if (propertyName.Length > 2 && propertyName[propertyName.Length - 2] == '.')
        {
            char last = propertyName[propertyName.Length - 1];
            if (last == 'x' || last == 'y' || last == 'z' || last == 'w')
                return propertyName.Substring(0, propertyName.Length - 2);
        }
        return propertyName;
    }

    private static string NicifyPropertyName(string baseName)
    {
        string p = baseName.StartsWith("m_") ? baseName.Substring(2) : baseName;
        if (p.Length > 0) p = char.ToUpperInvariant(p[0]) + p.Substring(1);
        return p;
    }

    // Раньше DisplayName склеивал ПОЛНЫЙ путь + тип + свойство в одну строку —
    // на узкой колонке (LABEL_W) это обрезалось до неотличимых друг от друга
    // "RightTransFormer/Right/ElbowR P..." для двух РАЗНЫХ свойств одного и того
    // же дочернего объекта (например Position и Rotation): разница была как раз
    // в хвосте строки, который срезался. Родной Animation window вместо этого
    // показывает только последний сегмент пути ("ElbowR Pivot") с отступом по
    // глубине иерархии, а не весь путь целиком — так и делаем здесь.
    private static (string display, int depth) BuildPropertyLabel(string path, Type type, string baseName)
    {
        string lastSegment = "";
        int depth = 0;
        if (!string.IsNullOrEmpty(path))
        {
            var segments = path.Split('/');
            lastSegment = segments[segments.Length - 1];
            depth = segments.Length; // 1 = прямой child корня, 2 = внук и т.д.
        }

        string niceProp = NicifyPropertyName(baseName);
        // Для Transform тип не показываем — и так понятно, что Position/Rotation/Scale
        // это трансформ; для остального (SpriteRenderer, MeshRenderer и т.п.) тип
        // оставляем, иначе "Sprite" сам по себе не объясняет, что именно меняется.
        string propPart = (type == typeof(Transform)) ? niceProp : $"{(type != null ? type.Name : "?")}.{niceProp}";

        string display = string.IsNullOrEmpty(lastSegment) ? propPart : $"{lastSegment}: {propPart}";
        return (display, depth);
    }

    private List<PropertyGroup> CollectPropertyGroups(AnimationClip clip)
    {
        var result = new List<PropertyGroup>();
        if (clip == null) return result;

        var indexByKey = new Dictionary<string, int>();

        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            string baseName = StripAxisSuffix(b.propertyName);
            string key = b.path + "|" + (b.type != null ? b.type.Name : "") + "|" + baseName;

            if (indexByKey.TryGetValue(key, out int existingIdx))
            {
                result[existingIdx].FloatBindings.Add(b);
                continue;
            }

            var (display, depth) = BuildPropertyLabel(b.path, b.type, baseName);
            indexByKey[key] = result.Count;
            result.Add(new PropertyGroup
            {
                DisplayName       = display,
                Path              = b.path,
                Depth             = depth,
                IsObjectReference = false,
                FloatBindings     = new List<EditorCurveBinding> { b }
            });
        }

        foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            var (display, depth) = BuildPropertyLabel(b.path, b.type, b.propertyName);
            result.Add(new PropertyGroup
            {
                DisplayName       = display,
                Path              = b.path,
                Depth             = depth,
                IsObjectReference = true,
                FloatBindings     = new List<EditorCurveBinding>(),
                ObjectBinding     = b
            });
        }

        // Сортируем по пути (родитель — строкой раньше своих потомков, т.к. путь
        // потомка всегда начинается с пути родителя + "/"), а внутри одного пути —
        // по имени свойства, чтобы Position/Rotation не прыгали местами между кадрами.
        result.Sort((x, y) =>
        {
            int c = string.Compare(x.Path, y.Path, StringComparison.Ordinal);
            return c != 0 ? c : string.Compare(x.DisplayName, y.DisplayName, StringComparison.Ordinal);
        });
        return result;
    }

    // Времена ключей конкретной property-группы (а не всего клипа сразу).
    private HashSet<float> GroupTimes(AnimationClip clip, PropertyGroup g)
    {
        var set = new HashSet<float>();
        if (!g.IsObjectReference)
        {
            foreach (var b in g.FloatBindings)
            {
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                if (curve == null) continue;
                foreach (var k in curve.keys)
                    set.Add(Mathf.Round(k.time * 1000f) / 1000f);
            }
        }
        else
        {
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, g.ObjectBinding);
            if (keys != null)
                foreach (var k in keys)
                    set.Add(Mathf.Round(k.time * 1000f) / 1000f);
        }
        return set;
    }

    // Зона под курсором внутри строки трека:
    // -2 = шапка (свёрнутая объединённая лента — старое поведение по всем биндингам),
    // -1 = не над лентой ключей вообще (блок настроек клипа, заголовок "Свойства" и т.п.),
    // >=0 = индекс конкретной property-группы под раскрытой стрелочкой.
    private int PropertyRowZoneAt(float rowLocalY, AnimationClip clip)
    {
        if (rowLocalY < TRACK_H) return -2;
        float y = rowLocalY - TRACK_H;
        if (y < SETTINGS_BLOCK_H) return -1;
        y -= SETTINGS_BLOCK_H;
        if (y < PROPERTIES_HEADER_H) return -1;
        y -= PROPERTIES_HEADER_H;
        int idx = Mathf.FloorToInt(y / PROPERTY_ROW_H);
        var groups = CollectPropertyGroups(clip);
        if (idx < 0 || idx >= groups.Count) return -1;
        return idx;
    }

    // ═══════════════════════════════════════════════════
    // NATIVE ICONS / TRACK COLORS  (визуальный пасс под Animation window)
    // ═══════════════════════════════════════════════════

    private static class Icons
    {
        private static GUIContent _play, _record, _first, _prev, _next, _last;
        public static GUIContent Play    => _play   ??= Safe("Animation.Play",    "▶");
        public static GUIContent Record  => _record ??= Safe("Animation.Record",  "●");
        public static GUIContent First   => _first  ??= Safe("Animation.FirstKey","|◀");
        public static GUIContent Prev    => _prev   ??= Safe("Animation.PrevKey", "◀");
        public static GUIContent Next    => _next   ??= Safe("Animation.NextKey", "▶");
        public static GUIContent Last    => _last   ??= Safe("Animation.LastKey", "▶|");

        // На случай, если имя иконки поменяется между версиями редактора — не роняем окно
        private static GUIContent Safe(string builtinName, string fallbackText)
        {
            try
            {
                var c = EditorGUIUtility.IconContent(builtinName);
                if (c != null && c.image != null) return c;
            }
            catch { /* ignore */ }
            return new GUIContent(fallbackText);
        }
    }

    // Палитра под цвет-кодирование треков (как разноцветные кривые в Curves-режиме нативного окна)
    private static readonly Color[] TrackColors =
    {
        new Color(0.98f, 0.42f, 0.42f), new Color(0.98f, 0.75f, 0.35f),
        new Color(0.55f, 0.92f, 0.45f), new Color(0.35f, 0.78f, 0.98f),
        new Color(0.72f, 0.55f, 0.98f), new Color(0.98f, 0.55f, 0.85f),
    };
    private static Color ColorForPart(int index) => TrackColors[index % TrackColors.Length];

    // Частота кадров берём с первого попавшегося клипа выбранного стейта —
    // этого достаточно, чтобы показывать реальные номера кадров, а не
    // округлять всё до целых секунд (у анимаций длиной в доли секунды
    // формат "минуты:секунды" бесполезен — все клипы показывали бы 0:00).
    //
    // ВАЖНО: разные парты могут использовать клипы с РАЗНЫМ fps (например,
    // Torso — 24, а Legs — 30). Одну общую линейку на все треки всё равно
    // приходится рисовать, поэтому в качестве референсной берём МАКСИМАЛЬНУЮ
    // частоту среди всех клипов стейта: тогда шаг в 1 кадр (стрелки,
    // Alt+←/→, снаппинг при записи) никогда не "перепрыгивает" кадры самого
    // частого клипа — а клипы с более низким fps просто держат текущий кадр
    // несколько тиков подряд, что визуально корректно и ничего не теряет.
    private float ReferenceFrameRate
    {
        get
        {
            float max = 0f;
            if (_selectedState?.parts != null)
                foreach (var p in _selectedState.parts)
                    if (p.clip != null) max = Mathf.Max(max, p.clip.frameRate);
            return max > 0f ? max : 30f;
        }
    }

    // true, если среди партов стейта есть клипы с РАЗНЫМ fps — используется,
    // чтобы показать предупреждение в UI. Смешивание частот в одном стейте
    // само по себе не ошибка, но может давать небольшой дрейф синхронизации
    // между партами, если об этом не думать заранее.
    private bool HasMixedFrameRates
    {
        get
        {
            if (_selectedState?.parts == null) return false;
            float? first = null;
            foreach (var p in _selectedState.parts)
            {
                if (p.clip == null) continue;
                if (first == null) { first = p.clip.frameRate; continue; }
                if (!Mathf.Approximately(first.Value, p.clip.frameRate)) return true;
            }
            return false;
        }
    }

    // Текст подсказки для предупреждения о разном fps — перечисляет per-part
    // значения, чтобы сразу было видно, какой конкретно клип выбивается.
    private string BuildFrameRateTooltip()
    {
        if (_selectedState?.parts == null) return "";
        var sb = new System.Text.StringBuilder("Разный FPS у клипов в этом стейте:\n");
        foreach (var p in _selectedState.parts)
            if (p.clip != null)
                sb.Append(p.partName).Append(": ").Append(p.clip.frameRate.ToString("0.##")).Append(" fps\n");
        sb.Append("\nШаг по кадру (←/→, Alt+←/→) считается по максимальному FPS среди них (")
          .Append(ReferenceFrameRate.ToString("0.##"))
          .Append("), чтобы не перепрыгивать кадры самого частого клипа.");
        return sb.ToString();
    }

    // Формат "секунды:кадры" — как в нативном Animation window Unity,
    // а не "минуты:секунды". Кадр — это номер кадра ВНУТРИ текущей секунды,
    // считается по ReferenceFrameRate (см. выше).
    private string FormatTime(float t)
    {
        float fps      = ReferenceFrameRate;
        int   fpsInt   = Mathf.Max(1, Mathf.RoundToInt(fps));
        int   totalFrm = Mathf.Max(0, Mathf.RoundToInt(t * fps));
        int   sec      = totalFrm / fpsInt;
        int   frm      = totalFrm % fpsInt;
        return $"{sec}:{frm:00}";
    }

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

        // Раньше: contentH = RULER_H + count * TRACK_H — все строки одной высоты.
        // Теперь строки с раскрытыми настройками клипа выше обычных, поэтому
        // считаем кумулятивные Y-смещения и кэшируем их в _lastRowTops — это
        // используется дальше и в отрисовке, и в обработке ввода (клики по ключам,
        // box-select), которым нужно знать, на какую строку попал курсор.
        var rowTops = ComputeRowTops(count);
        _lastRowTops = rowTops;
        float tracksTotalH = rowTops[count];
        float contentH     = RULER_H + tracksTotalH;

        // Не считаем ширину вручную через currentViewWidth — Odin оборачивает контент
        // в свой скролл-контейнер с отступами, и currentViewWidth его не учитывает.
        // Просим layout выделить всю оставшуюся ширину сам — тогда сумма (таймлайн + сайдпанель)
        // никогда не превысит то, что реально есть в окне, и сайдпанель не будет вылезать за край.
        //
        // ExpandHeight здесь ключевой момент: раньше area сайзилась строго под contentH,
        // и если реальных треков было мало — под ними оставалась пустая незакрашенная
        // область до низа окна. С ExpandHeight Unity (двухпроходный layout) сам досчитает
        // area.height до реального остатка колонки, и фон EditorGUI.DrawRect(area, ...)
        // ниже закроет всю эту область, а не только contentH.
        Rect area = GUILayoutUtility.GetRect(0, contentH,
            GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        float contentW = area.width;

        Rect rulerRect  = new Rect(area.x + LABEL_W, area.y, area.width - LABEL_W, RULER_H);
        Rect labelsRect = new Rect(area.x, area.y + RULER_H, LABEL_W, tracksTotalH);
        Rect tracksRect = new Rect(area.x + LABEL_W, area.y + RULER_H,
                                    area.width - LABEL_W, tracksTotalH);
        Rect fullRect   = new Rect(area.x + LABEL_W, area.y, area.width - LABEL_W, contentH);

        _lastTracksRect = tracksRect;

        // area.height теперь может быть больше contentH — фон закрывает весь остаток колонки
        EditorGUI.DrawRect(area, new Color(0.157f, 0.157f, 0.157f));

        DrawRuler(rulerRect);
        DrawTrackLabels(labelsRect, count, rowTops);
        DrawTracksContent(tracksRect, count, rowTops);
        DrawBoxSelectRect(tracksRect);
        DrawPlayhead(fullRect);

        HandleTimelineInput(area, tracksRect);

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

        DrawBottomBar();
    }

    // Нижняя строка: переключатель Dopesheet/Curves (как в нативном окне) + статус выделения
    [NonSerialized] private bool _curvesMode = false;

    private void DrawBottomBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        var tabStyle = EditorStyles.toolbarButton;
        var prevBg = GUI.backgroundColor;

        GUI.backgroundColor = !_curvesMode ? new Color(0.45f, 0.7f, 1f) : prevBg;
        if (GUILayout.Toggle(!_curvesMode, "Dopesheet", tabStyle, GUILayout.Width(70)))
            _curvesMode = false;
        GUI.backgroundColor = prevBg;

        GUI.backgroundColor = _curvesMode ? new Color(0.45f, 0.7f, 1f) : prevBg;
        if (GUILayout.Toggle(_curvesMode, "Curves", tabStyle, GUILayout.Width(60)))
            _curvesMode = true;
        GUI.backgroundColor = prevBg;

        GUILayout.Space(10);
        GUILayout.Label($"{FormatTime(_currentTime)}", EditorStyles.miniLabel, GUILayout.Width(40));
        if (_selectedKeys.Count > 0)
            GUILayout.Label($"[{_selectedKeys.Count} ключей]", EditorStyles.miniLabel);

        GUILayout.FlexibleSpace();
        GUILayout.Label("Space=Play  Ctrl+S=Save  Del=Delete  Alt+←/→=к ключу", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        if (_curvesMode)
            EditorGUILayout.HelpBox("Режим Curves ещё не реализован — задел на будущее.", MessageType.None);
    }

    // ═══════════════════════════════════════════════════
    // TOOLBAR
    // ═══════════════════════════════════════════════════

    private void DrawTimelineToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = _isRecording ? new Color(1f, 0.25f, 0.25f) : prevBg;
        if (GUILayout.Button(Icons.Record, EditorStyles.toolbarButton, GUILayout.Width(30)))
            ToggleRecording();
        GUI.backgroundColor = prevBg;

        GUILayout.Space(4);

        if (GUILayout.Button(Icons.First, EditorStyles.toolbarButton, GUILayout.Width(24))) SetTime(0f);
        if (GUILayout.Button(Icons.Prev,  EditorStyles.toolbarButton, GUILayout.Width(24))) StepToPrevKey();

        var playBg = GUI.backgroundColor;
        GUI.backgroundColor = _isPlaying ? new Color(0.4f, 0.75f, 1f) : playBg;
        if (GUILayout.Button(Icons.Play, EditorStyles.toolbarButton, GUILayout.Width(24)))
        {
            if (_isPlaying) StopPreview(); else StartPreview();
        }
        GUI.backgroundColor = playBg;

        if (GUILayout.Button(Icons.Next, EditorStyles.toolbarButton, GUILayout.Width(24))) StepToNextKey();
        if (GUILayout.Button(Icons.Last, EditorStyles.toolbarButton, GUILayout.Width(24))) SetTime(GetMaxClipLength());

        GUILayout.Space(8);

        var timeFieldStyle = new GUIStyle(EditorStyles.toolbarTextField)
            { alignment = TextAnchor.MiddleCenter };
        string typed = EditorGUILayout.DelayedTextField(FormatTime(_currentTime), timeFieldStyle, GUILayout.Width(50));
        if (typed != FormatTime(_currentTime) && TryParseTime(typed, out float parsedT))
            SetTime(parsedT);

        GUILayout.FlexibleSpace();

        GUI.enabled = _selectedKeys.Count > 0;
        if (GUILayout.Button("Copy", EditorStyles.toolbarButton, GUILayout.Width(46)))
            CopyKeyframes();
        GUI.enabled = _clipboard.Count > 0;
        if (GUILayout.Button("Paste", EditorStyles.toolbarButton, GUILayout.Width(46)))
            PasteKeyframes();
        GUI.enabled = _selectedKeys.Count > 0;
        if (GUILayout.Button("Delete", EditorStyles.toolbarButton, GUILayout.Width(50)))
            DeleteSelectedKeyframes();
        GUI.enabled = true;

        GUILayout.Space(8);
        GUILayout.Label("Zoom", EditorStyles.miniLabel, GUILayout.Width(32));
        // Раньше макс. зум был 900 — на длинных клипах этого не хватало, чтобы
        // растащить соседние ключи на расстояние, удобное для клика/драга.
        float nz = GUILayout.HorizontalSlider(_zoom, 40f, 4000f, GUILayout.Width(80));
        if (!Mathf.Approximately(nz, _zoom)) { _zoom = nz; Repaint(); }

        EditorGUILayout.EndHorizontal();

        DrawStateSelectorBar();
    }

    // Тонкая строка-комбобокс под тулбаром — аналог выпадающего списка "Eat" в нативном окне.
    // Само меню теперь общее с сайдпанелью — см. ShowStateDropdownMenu().
    private void DrawStateSelectorBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        string label = _selectedState != null ? _selectedState.stateName : "— нет стейта —";
        if (GUILayout.Button(label, EditorStyles.toolbarPopup, GUILayout.Width(180)))
            ShowStateDropdownMenu();

        GUILayout.Space(6);

        // Preview — как в нативном Animator/Timeline окне: отдельный тумблер,
        // независимый от Play. Play сам включает Preview при необходимости
        // (см. StartPreview — там AnimationMode.StartAnimationMode(), если ещё не включён),
        // поэтому кнопка сама "загорается", когда нажимаешь Play — ничего
        // дополнительно связывать не нужно, оба завязаны на один и тот же
        // AnimationMode.InAnimationMode(). Выключение — возвращает дефолтную позу
        // персонажа (это встроенное поведение AnimationMode.StopAnimationMode()).
        bool previewOn = AnimationMode.InAnimationMode();
        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = previewOn ? new Color(0.4f, 0.75f, 1f) : prevBg;
        bool newPreviewOn = GUILayout.Toggle(previewOn, "Preview", EditorStyles.toolbarButton, GUILayout.Width(60));
        GUI.backgroundColor = prevBg;
        if (newPreviewOn != previewOn)
            TogglePreviewMode();

        // Предупреждение о разном fps среди клипов текущего стейта — раньше
        // HasMixedFrameRates считался, но нигде не показывался пользователю.
        // Само по себе смешение fps не баг, но стоит явно об этом сигналить,
        // а не оставлять как невидимый сюрприз.
        if (_selectedState != null && HasMixedFrameRates)
        {
            GUILayout.Space(6);
            var prevContent = GUI.contentColor;
            GUI.contentColor = new Color(1f, 0.75f, 0.35f);
            GUILayout.Label(new GUIContent("⚠ разный FPS", BuildFrameRateTooltip()), EditorStyles.miniLabel);
            GUI.contentColor = prevContent;
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void TogglePreviewMode()
    {
        if (AnimationMode.InAnimationMode())
        {
            // Выключаем: если в этот момент играло — тоже останавливаем (иначе Tick()
            // продолжит пытаться сэмплить кадры в выключенном AnimationMode впустую,
            // а кнопка Play будет молча врать, что всё ещё играет).
            // AnimationMode.StopAnimationMode() сам возвращает все засэмпленные
            // свойства к их состоянию до превью — это и есть "дефолтная поза".
            StopPreview();
        }
        else
        {
            AnimationMode.StartAnimationMode();
            SampleAll(_currentTime); // сразу показываем текущий кадр, а не T-позу
        }
        Repaint();
    }

    // Общее меню выбора стейта — переиспользуется тулбаром таймлайна и сайдпанелью,
    // чтобы не дублировать один и тот же GenericMenu в двух местах.
    private void ShowStateDropdownMenu()
    {
        var menu   = new GenericMenu();
        var states = _activeConfig != null ? _activeConfig.states : null;
        if (states != null)
        {
            for (int i = 0; i < states.Count; i++)
            {
                var s = states[i];
                if (s == null) continue;
                int idx = i;
                menu.AddItem(new GUIContent(s.stateName), s == _selectedState, () =>
                {
                    StopPreview();
                    _selectedIndex = idx;
                    _selectedState = s;
                    _stateEditor   = null;
                    OnStateSelected?.Invoke(s);
                    Repaint();
                });
            }
        }
        // Раньше "+ Добавить" / "− Удалить" были отдельными кнопками в сайдпанели.
        // Теперь сайдпанель без них, поэтому оба действия переехали сюда, в конец меню.
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("+ Новый стейт"), false, AddState);

        if (_selectedState != null)
            menu.AddItem(new GUIContent($"− Удалить «{_selectedState.stateName}»"), false, RemoveSelectedState);
        else
            menu.AddDisabledItem(new GUIContent("− Удалить"));

        menu.ShowAsContext();
    }

    // Парсит "секунды:кадры" (тот же формат, что выдаёт FormatTime) обратно в секунды.
    private bool TryParseTime(string s, out float seconds)
    {
        seconds = 0f;
        if (string.IsNullOrEmpty(s)) return false;
        var parts = s.Split(':');
        if (parts.Length == 2
            && int.TryParse(parts[0], out int sec)
            && int.TryParse(parts[1], out int frm))
        {
            // Было "CurrentFrameRate" — такого свойства не существует, это не компилировалось.
            // Парсим кадры по той же ReferenceFrameRate, по которой их печатает FormatTime,
            // иначе ввод "0:12" интерпретировался бы по другому fps, чем показывается.
            seconds = sec + frm / ReferenceFrameRate;
            return true;
        }
        return float.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out seconds);
    }

    private void StepToPrevKey()
    {
        if (_selectedState == null) return;
        bool found = false; float best = 0f;
        foreach (var p in _selectedState.parts)
        {
            if (p.clip == null) continue;
            foreach (var t in CollectUniqueTimes(p.clip))
                if (t < _currentTime - 0.0005f && (!found || t > best)) { best = t; found = true; }
        }
        if (found) SetTime(best);
    }

    private void StepToNextKey()
    {
        if (_selectedState == null) return;
        bool found = false; float best = GetMaxClipLength();
        foreach (var p in _selectedState.parts)
        {
            if (p.clip == null) continue;
            foreach (var t in CollectUniqueTimes(p.clip))
                if (t > _currentTime + 0.0005f && (!found || t < best)) { best = t; found = true; }
        }
        if (found) SetTime(best);
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
                GUI.Label(new Rect(x + 2f, 1f, 60f, rect.height), FormatTime(t), textStyle);
        }
        GUI.EndClip();
    }

    // ═══════════════════════════════════════════════════
    // TRACK LABELS
    // ═══════════════════════════════════════════════════

    private static GUIStyle _foldoutGlyphStyle;

    // Выделяет в иерархии GameObject, на котором висит Animator этого парта,
    // и пингует его (та же подсветка, что при клике на ссылку в инспекторе).
    private void SelectPartInHierarchy(string partName)
    {
        if (string.IsNullOrEmpty(partName)) return;
        if (!AnimatorRegistry.Animators.TryGetValue(partName, out var animator) || animator == null)
            return;

        Selection.activeGameObject = animator.gameObject;
        EditorGUIUtility.PingObject(animator.gameObject);
    }

    private void DrawTrackLabels(Rect rect, int count, float[] rowTops)
    {
        EditorGUI.DrawRect(rect, new Color(0.165f, 0.165f, 0.165f));

        _foldoutGlyphStyle ??= new GUIStyle(EditorStyles.label)
        {
            normal    = { textColor = new Color(0.58f, 0.58f, 0.58f) },
            fontSize  = 9,
            alignment = TextAnchor.MiddleCenter
        };
        var nameStyle = new GUIStyle(EditorStyles.label)
        {
            normal    = { textColor = new Color(0.82f, 0.82f, 0.82f) },
            padding   = new RectOffset(2, 4, 0, 0),
            alignment = TextAnchor.MiddleLeft,
            fontSize  = 11,
            clipping  = TextClipping.Clip
        };
        // Тот же стиль, но с цветом ссылки — показываем при наведении на имя парта,
        // чтобы было видно, что по нему можно кликнуть (как обычная ссылка).
        var nameStyleHover = new GUIStyle(nameStyle)
        {
            normal = { textColor = new Color(0.55f, 0.75f, 1f) }
        };
        var clipStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal    = { textColor = new Color(0.5f, 0.5f, 0.5f) },
            padding   = new RectOffset(4, 4, 0, 0),
            alignment = TextAnchor.MiddleRight,
            fontSize  = 9,
            clipping  = TextClipping.Clip
        };

        _propsHeaderStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            normal   = { textColor = new Color(0.55f, 0.55f, 0.55f) },
            fontSize = 9
        };
        _propGlyphStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            normal    = { textColor = new Color(0.5f, 0.5f, 0.5f) },
            fontSize  = 8,
            alignment = TextAnchor.MiddleCenter
        };
        _propNameStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            normal    = { textColor = new Color(0.78f, 0.78f, 0.78f) },
            fontSize  = 9,
            clipping  = TextClipping.Clip
        };

        const float FOLDOUT_W = 16f;
        const float DOT_W     = 14f;

        for (int i = 0; i < count; i++)
        {
            float rowH = rowTops[i + 1] - rowTops[i];
            Rect  row       = new Rect(rect.x, rect.y + rowTops[i], rect.width, rowH);
            Rect  headerRow = new Rect(row.x, row.y, row.width, TRACK_H); // верхняя строка — как раньше

            if (i % 2 == 1) EditorGUI.DrawRect(row, new Color(1, 1, 1, 0.02f));
            EditorGUI.DrawRect(new Rect(row.x, row.yMax - 1f, row.width, 1f),
                new Color(0f, 0f, 0f, 0.35f));

            bool expanded = _expandedTracks.Contains(i);

            // Фолдаут теперь реально кликабелен — раньше "▸" был просто картинкой.
            // Разворачивает/сворачивает мини-панель настроек клипа под строкой.
            Rect foldoutRect = new Rect(headerRow.x + 3f, headerRow.y, FOLDOUT_W, headerRow.height);
            GUI.Label(foldoutRect, expanded ? "▾" : "▸", _foldoutGlyphStyle);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                && foldoutRect.Contains(Event.current.mousePosition))
            {
                if (expanded) _expandedTracks.Remove(i);
                else          _expandedTracks.Add(i);
                Event.current.Use();
                Repaint();
                // rowTops этого кадра теперь устарели (высота строки изменилась) —
                // но перерисуется на следующем Repaint(), поэтому ничего досчитывать не нужно.
            }

            // точка-цвет — свой отдельный участок у правого края
            Rect dotRect = new Rect(headerRow.xMax - DOT_W, headerRow.y + headerRow.height * 0.5f - 3f, 6f, 6f);

            // имя парта и имя клипа делят оставшееся место между собой БЕЗ наложения —
            // раньше оба рисовались на одном row и налезали друг на друга при длинных именах
            float usable   = Mathf.Max(0f, headerRow.width - foldoutRect.width - 3f - DOT_W - 4f);
            Rect  nameRect = new Rect(foldoutRect.xMax, headerRow.y, usable * 0.55f, headerRow.height);
            Rect  clipRect = new Rect(nameRect.xMax, headerRow.y, usable * 0.45f, headerRow.height);

            var part = _selectedState.parts[i];
            // GUIContent с тем же текстом как tooltip — если имя обрежется по ширине,
            // полное имя всё равно можно увидеть, наведя мышь. Если fps клипа отличается
            // от референсного (ReferenceFrameRate) — добавляем это в tooltip, чтобы было
            // сразу видно, у какого именно парта "особый" fps.
            bool nameHover = nameRect.Contains(Event.current.mousePosition);
            GUI.Label(nameRect, new GUIContent(part.partName, part.partName),
                nameHover ? nameStyleHover : nameStyle);
            EditorGUIUtility.AddCursorRect(nameRect, MouseCursor.Link);

            // Клик по имени парта — выделяет соответствующий GameObject в иерархии
            // и пингует его в окне Project/Hierarchy, как обычная ссылка на объект.
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && nameHover)
            {
                SelectPartInHierarchy(part.partName);
                Event.current.Use();
            }

            if (part.clip != null)
            {
                string clipTooltip = part.clip.name;
                if (!Mathf.Approximately(part.clip.frameRate, ReferenceFrameRate))
                    clipTooltip += $"\n{part.clip.frameRate:0.##} fps (референс: {ReferenceFrameRate:0.##} fps)";
                GUI.Label(clipRect, new GUIContent(part.clip.name, clipTooltip), clipStyle);
            }

            EditorGUI.DrawRect(dotRect, ColorForPart(i));

            if (expanded)
            {
                Rect settingsRect = new Rect(row.x, headerRow.yMax, row.width, SETTINGS_BLOCK_H);
                DrawInlineClipSettings(settingsRect, part.clip);

                var groups = CollectPropertyGroups(part.clip);
                Rect propsHeaderRect = new Rect(row.x + 4f, settingsRect.yMax, row.width - 8f, PROPERTIES_HEADER_H);
                GUI.Label(propsHeaderRect,
                    groups.Count > 0 ? $"Свойства ({groups.Count}):" : "Нет анимируемых свойств",
                    _propsHeaderStyle);

                float py = propsHeaderRect.yMax;
                for (int g = 0; g < groups.Count; g++)
                {
                    Rect propRowRect = new Rect(row.x, py, row.width, PROPERTY_ROW_H);
                    if (g % 2 == 1) EditorGUI.DrawRect(propRowRect, new Color(1f, 1f, 1f, 0.02f));

                    // Отступ по глубине иерархии (родитель → дочерний объект → внук…) —
                    // как в родном Animation window, вместо повторения полного пути
                    // текстом на каждой строке (которое просто обрезалось на узкой панели).
                    float indent = Mathf.Min(groups[g].Depth * 8f, propRowRect.width * 0.5f);

                    // Ромб — обычная float-кривая, квадрат — object-reference (смена
                    // спрайта/материала), тот же язык значков, что и на самой ленте ключей.
                    Rect glyphRect = new Rect(propRowRect.x + 6f + indent, propRowRect.y, 10f, propRowRect.height);
                    GUI.Label(glyphRect, groups[g].IsObjectReference ? "■" : "◆", _propGlyphStyle);

                    Rect propNameRect = new Rect(glyphRect.xMax + 2f, propRowRect.y,
                        propRowRect.width - glyphRect.width - indent - 12f, propRowRect.height);
                    GUI.Label(propNameRect,
                        new GUIContent(groups[g].DisplayName, groups[g].DisplayName), _propNameStyle);

                    py += PROPERTY_ROW_H;
                }
            }
        }
    }

    private static GUIStyle _propsHeaderStyle;
    private static GUIStyle _propGlyphStyle;
    private static GUIStyle _propNameStyle;

    // Мини-панель настроек клипа под раскрытой стрелочкой: FPS, Loop Time, Loop Pose.
    // Это самые ходовые настройки для геймдев-цикла: FPS влияет на плотность кадров
    // при записи/скраббинге, Loop Time включает бесшовный луп проигрывания в Animator,
    // Loop Pose (доступен только при включённом Loop Time) сглаживает позу на стыке
    // конца и начала клипа, чтобы не было "скачка" при зацикливании.
    private void DrawInlineClipSettings(Rect rect, AnimationClip clip)
    {
        var labelStyle = new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = new Color(0.65f, 0.65f, 0.65f) } };

        if (clip == null)
        {
            GUI.Label(new Rect(rect.x + 4f, rect.y + 2f, rect.width - 8f, 16f),
                "Клип не назначен", labelStyle);
            return;
        }

        const float ROW_H = 16f;
        const float PAD   = 3f;
        float y = rect.y + 2f;

        // FPS
        Rect fpsLabelRect = new Rect(rect.x + 4f, y, 28f, ROW_H);
        Rect fpsFieldRect = new Rect(fpsLabelRect.xMax, y, rect.width - fpsLabelRect.width - 8f, ROW_H);
        GUI.Label(fpsLabelRect, "FPS", labelStyle);
        EditorGUI.BeginChangeCheck();
        float newFps = EditorGUI.FloatField(fpsFieldRect, clip.frameRate);
        if (EditorGUI.EndChangeCheck() && newFps > 0f)
        {
            Undo.RecordObject(clip, "Change Clip Frame Rate");
            clip.frameRate = newFps;
            EditorUtility.SetDirty(clip);
        }
        y += ROW_H + PAD;

        var settings = AnimationUtility.GetAnimationClipSettings(clip);

        // Loop Time
        Rect loopRect = new Rect(rect.x + 4f, y, rect.width - 8f, ROW_H);
        EditorGUI.BeginChangeCheck();
        bool newLoopTime = EditorGUI.ToggleLeft(loopRect, "Loop Time", settings.loopTime);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(clip, "Toggle Loop Time");
            settings.loopTime = newLoopTime;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }
        y += ROW_H + PAD;

        // Loop Pose — имеет смысл только при включённом Loop Time, поэтому
        // блокируем контрол, а не прячем: так видно, что опция вообще есть.
        GUI.enabled = settings.loopTime;
        Rect poseRect = new Rect(rect.x + 4f, y, rect.width - 8f, ROW_H);
        EditorGUI.BeginChangeCheck();
        bool newLoopPose = EditorGUI.ToggleLeft(poseRect, "Loop Pose", settings.loopBlend);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(clip, "Toggle Loop Pose");
            settings.loopBlend = newLoopPose;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }
        GUI.enabled = true;
    }

    // ═══════════════════════════════════════════════════
    // TRACKS CONTENT
    // ═══════════════════════════════════════════════════

    private void DrawTracksContent(Rect rect, int count, float[] rowTops)
    {
        EditorGUI.DrawRect(rect, new Color(0.14f, 0.14f, 0.14f));
        GUI.BeginClip(rect);

        DrawGridLines(rect.width, rect.height);

        for (int i = 0; i < count; i++)
        {
            float rowH = rowTops[i + 1] - rowTops[i];
            Rect  row    = new Rect(0, rowTops[i], rect.width, rowH);
            // Ключи и полоска клипа рисуются только в верхней TRACK_H-полосе строки —
            // остальное (если строка раскрыта) просто фон-заполнитель под панель
            // настроек в колонке labels слева.
            Rect  keyRow = new Rect(0, rowTops[i], rect.width, TRACK_H);

            if (i % 2 == 1) EditorGUI.DrawRect(row, new Color(1, 1, 1, 0.022f));
            EditorGUI.DrawRect(new Rect(0, row.yMax - 1f, rect.width, 1f),
                new Color(0f, 0f, 0f, 0.3f));

            var clip = _selectedState.parts[i].clip;
            if (clip == null) continue;

            // Полоска клипа
            float cw = Mathf.Max(0f, T2X(clip.length));
            if (cw > 0f)
            {
                var bar = new Rect(0, keyRow.y + 4f, cw, keyRow.height - 8f);
                EditorGUI.DrawRect(bar, new Color(0.22f, 0.42f, 0.65f, 0.28f));
                DrawRectOutline(bar, new Color(0.28f, 0.52f, 0.82f, 0.5f));
            }

            DrawKeyframesOnRow(keyRow, clip, i, rect.width);

            // Раскрытая строка — под лентой-шапкой рисуем по одной мини-ленте
            // ключей на каждое анимируемое свойство, выровненной по тем же
            // py-координатам, что и список названий в labels-колонке.
            if (_expandedTracks.Contains(i))
            {
                var groups = CollectPropertyGroups(clip);
                float py = keyRow.yMax + SETTINGS_BLOCK_H + PROPERTIES_HEADER_H;
                for (int g = 0; g < groups.Count; g++)
                {
                    Rect propRow = new Rect(0, py, rect.width, PROPERTY_ROW_H);
                    if (g % 2 == 1) EditorGUI.DrawRect(propRow, new Color(1f, 1f, 1f, 0.02f));
                    DrawPropertyKeyframes(propRow, clip, groups[g], i);
                    py += PROPERTY_ROW_H;
                }
            }
        }

        GUI.EndClip();
    }

    // Мини-лента ключей одного конкретного анимируемого свойства (внутри
    // раскрытой строки) — именно она и решает проблему "слипшихся" ключей:
    // раз каждое свойство теперь на своей строке, два разных свойства,
    // закеенных в один момент времени, больше не рисуются друг на друге.
    private void DrawPropertyKeyframes(Rect row, AnimationClip clip, PropertyGroup group, int trackIndex)
    {
        float cy = row.y + row.height * 0.5f;
        Color baseColor = ColorForPart(trackIndex);

        foreach (float t in GroupTimes(clip, group))
        {
            float x = T2X(t);
            if (x < -DIAMOND - 1f || x > row.width + DIAMOND + 1f) continue;

            bool sel = _selectedKeys.Exists(k => ReferenceEquals(k.Clip, clip)
                && k.IsObjectReference == group.IsObjectReference
                && Mathf.Abs(k.Time - t) < 0.001f
                && (group.IsObjectReference
                    ? k.Binding.Equals(group.ObjectBinding)
                    : group.FloatBindings.Exists(b => b.Equals(k.Binding))));

            if (group.IsObjectReference)
                DrawSpriteKeyMarker(new Vector2(x, cy), sel, baseColor);
            else
                DrawDiamond(new Vector2(x, cy), sel, baseColor);
        }
    }

    private void DrawKeyframesOnRow(Rect row, AnimationClip clip, int trackIndex, float trackW)
    {
        float cy  = row.y + row.height * 0.5f;
        Color baseColor = ColorForPart(trackIndex);

        // Раньше тут просто объединялись все времена в один HashSet — если два
        // РАЗНЫХ свойства (например Position и смена спрайта) кеились в одну и
        // ту же секунду, это давало один и тот же ромбик, и было не видно, что
        // там вообще что-то слиплось. Теперь считаем по группам (см.
        // CollectPropertyGroups) и, если в моменте времени сошлось больше одной
        // группы, рисуем маленький жёлтый бейдж-предупреждение сверху — сигнал
        // "тут не одно свойство, разверни строку, чтобы разделить".
        var groups = CollectPropertyGroups(clip);
        var timeGroupCount  = new Dictionary<float, int>();
        var timeHasObjectRef = new HashSet<float>();

        foreach (var g in groups)
        {
            foreach (float t in GroupTimes(clip, g))
            {
                timeGroupCount.TryGetValue(t, out int n);
                timeGroupCount[t] = n + 1;
                if (g.IsObjectReference) timeHasObjectRef.Add(t);
            }
        }

        foreach (var kv in timeGroupCount)
        {
            float t = kv.Key;
            float x = T2X(t);
            if (x < -DIAMOND - 1f || x > trackW + DIAMOND + 1f) continue;

            bool sel = _selectedKeys.Exists(
                k => ReferenceEquals(k.Clip, clip) && Mathf.Abs(k.Time - t) < 0.001f);

            // Ключи смены спрайта/материала (object reference curve) рисуем квадратом —
            // так их сразу видно отдельно от обычных float-ключей (позиция, поворот и т.д.)
            if (timeHasObjectRef.Contains(t))
                DrawSpriteKeyMarker(new Vector2(x, cy), sel, baseColor);
            else
                DrawDiamond(new Vector2(x, cy), sel, baseColor);

            if (kv.Value > 1)
                DrawStackedKeyBadge(new Vector2(x, cy));
        }
    }

    // Времена ключей раздельно по типу кривой: обычные float-кривые (AnimationCurve)
    // и object-reference кривые — именно так Unity хранит смену спрайта/материала/
    // любого UnityEngine.Object на клипе. Раньше читались только float-кривые,
    // поэтому ключи смены спрайта были на таймлайне невидимы.
    private struct TrackKeyTimes
    {
        public HashSet<float> Float;
        public HashSet<float> ObjectRef;
    }

    private TrackKeyTimes CollectKeyTimes(AnimationClip clip)
    {
        var result = new TrackKeyTimes { Float = new HashSet<float>(), ObjectRef = new HashSet<float>() };

        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            var curve = AnimationUtility.GetEditorCurve(clip, b);
            if (curve == null) continue;
            foreach (var k in curve.keys)
                result.Float.Add(Mathf.Round(k.time * 1000f) / 1000f);
        }

        foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, b);
            if (keys == null) continue;
            foreach (var k in keys)
                result.ObjectRef.Add(Mathf.Round(k.time * 1000f) / 1000f);
        }

        return result;
    }

    // Объединённый набор — используется там, где неважен конкретный тип ключа
    // (шаг Prev/Next Key и т.п.)
    private HashSet<float> CollectUniqueTimes(AnimationClip clip)
    {
        var t = CollectKeyTimes(clip);
        t.Float.UnionWith(t.ObjectRef);
        return t.Float;
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

        // Тонкая белая линия на всю высоту дорожек, как в нативном Animation window
        EditorGUI.DrawRect(new Rect(x, RULER_H, 1f, rect.height - RULER_H),
            new Color(1f, 1f, 1f, 0.9f));

        // Компактный флажок-хэндл в зоне линейки (а не крупный треугольник)
        EditorGUI.DrawRect(new Rect(x - 4f, 1f, 8f, RULER_H - 5f), new Color(0.87f, 0.87f, 0.87f));
        EditorGUI.DrawRect(new Rect(x - 1f, RULER_H - 5f, 2f, 5f), new Color(0.87f, 0.87f, 0.87f));

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
            // Линейный шаг (- delta*12) на диапазоне 40..900 более-менее ощущался,
            // но при расширении диапазона до 4000 либо еле ползёт возле 40,
            // либо улетает за пару кадров возле верхней границы. Экспоненциальный
            // (мультипликативный) зум держит одинаковое ощущение "скорости"
            // на любом уровне зума — так же зумит родное Animation-окно Unity.
            float factor = Mathf.Pow(1.06f, -e.delta.y);
            _zoom = Mathf.Clamp(_zoom * factor, 40f, 4000f);
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
        int   idx    = RowIndexAtLocalY(localY, _selectedState.parts.Count);
        if (idx < 0 || idx >= _selectedState.parts.Count) return false;

        var part = _selectedState.parts[idx];
        if (part.clip == null) return false;

        float rowLocalY = localY - _lastRowTops[idx];
        bool  expanded  = _expandedTracks.Contains(idx);
        int   zone      = expanded ? PropertyRowZoneAt(rowLocalY, part.clip) : -2;

        // -1: клик в блоке настроек клипа / заголовке "Свойства" — там не лента ключей.
        if (zone == -1) return false;

        float localX = mousePos.x - tracksRect.x;

        // НАШЛИ БАГ, из-за которого один "ключевой кадр" сам собой распадался на
        // 2-3: раньше при клике по группе (Position/Rotation = x+y+z, три РАЗНЫХ
        // float-биндинга) мы хватали ПЕРВЫЙ подходящий биндинг (например только
        // .x) и на этом останавливались — .y и .z в выделение не попадали.
        // При драге двигалась только эта одна ось, а две другие оставались на
        // старом месте. В объединённом отображении группы (см. GroupTimes,
        // рисующей ленту по всем биндингам сразу) это выглядело как "было
        // 1 ключ — стало 2-3 в разных местах", хотя по факту x/y/z просто
        // разъехались по времени. Теперь при клике сначала находим ВРЕМЯ
        // клика по любому биндингу группы, а затем добавляем в выделение ВСЕ
        // биндинги этой группы, у которых есть ключ ровно в этот момент —
        // так x/y/z всегда двигаются вместе, как один keyframe.
        var candidateGroups = zone >= 0
            ? new List<PropertyGroup> { CollectPropertyGroups(part.clip)[zone] }
            : CollectPropertyGroups(part.clip); // zone == -2 — свёрнутая шапка, перебираем все группы клипа

        foreach (var group in candidateGroups)
        {
            if (!group.IsObjectReference)
            {
                float? foundTime = null;
                foreach (var binding in group.FloatBindings)
                {
                    var curve = AnimationUtility.GetEditorCurve(part.clip, binding);
                    if (curve == null) continue;
                    foreach (var key in curve.keys)
                    {
                        if (Mathf.Abs(T2X(key.time) - localX) <= 8f) { foundTime = key.time; break; }
                    }
                    if (foundTime.HasValue) break;
                }
                if (!foundTime.HasValue) continue; // эта группа не подошла — пробуем следующую (актуально для шапки)

                // Раньше клик ВСЕГДА сбрасывал текущее выделение (если не зажат Shift),
                // даже если кликнули по ключу, который уже был частью мульти-выделения
                // (например, набранного рамкой). Из-за этого драг всегда тащил только
                // ОДИН ключ — тот, за который непосредственно схватились, а остальные
                // выделенные ключи "отваливались" в момент клика, ещё до начала драга.
                // Теперь: если кликнутый ключ уже в выделении — ничего не трогаем,
                // просто разрешаем драг всей текущей группы выделенных ключей сразу.
                // Сброс выделения по-прежнему происходит по клику в пустое место
                // (см. HandleTimelineInput) или по клику на ключ, которого в выделении ещё нет.
                bool alreadySelected = group.FloatBindings.Exists(b => _selectedKeys.Exists(
                    k => ReferenceEquals(k.Clip, part.clip) && !k.IsObjectReference
                         && k.Binding.Equals(b) && Mathf.Abs(k.Time - foundTime.Value) < 0.001f));

                if (!Event.current.shift && !alreadySelected)
                    _selectedKeys.Clear();

                if (!alreadySelected)
                {
                    foreach (var binding in group.FloatBindings)
                    {
                        var curve = AnimationUtility.GetEditorCurve(part.clip, binding);
                        if (curve == null) continue;
                        int keyIdx = FindKeyByTime(curve, foundTime.Value);
                        if (keyIdx < 0) continue; // не у каждой оси обязательно есть ключ именно в этот момент
                        _selectedKeys.Add(new KeyframeRef
                            { Clip = part.clip, Binding = binding, Time = foundTime.Value, IsObjectReference = false });
                    }
                }
                Repaint();
                return true;
            }
            else
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(part.clip, group.ObjectBinding);
                if (keys == null) continue;

                float? foundTime = null;
                foreach (var key in keys)
                    if (Mathf.Abs(T2X(key.time) - localX) <= 8f) { foundTime = key.time; break; }
                if (!foundTime.HasValue) continue;

                bool alreadySelected = _selectedKeys.Exists(
                    k => ReferenceEquals(k.Clip, part.clip) && k.IsObjectReference
                         && k.Binding.Equals(group.ObjectBinding) && Mathf.Abs(k.Time - foundTime.Value) < 0.001f);

                if (!Event.current.shift && !alreadySelected)
                    _selectedKeys.Clear();

                if (!alreadySelected)
                {
                    _selectedKeys.Add(new KeyframeRef
                        { Clip = part.clip, Binding = group.ObjectBinding, Time = foundTime.Value, IsObjectReference = true });
                }
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

        // Раньше строки были одной высоты и диапазон затронутых строк считался
        // делением на TRACK_H. Теперь высота переменная, поэтому просто идём по
        // всем партам и проверяем пересечение бокса с лентой ключей КОНКРЕТНОЙ
        // строки (верхние TRACK_H пикселей от rowTops[i]) — раскрытая панель
        // настроек под строкой в выделение не должна попадать вообще.
        for (int i = 0; i < _selectedState.parts.Count; i++)
        {
            if (_lastRowTops == null || i + 1 >= _lastRowTops.Length) break;

            float rowTop        = _lastRowTops[i];
            float keyBandBottom = rowTop + TRACK_H;
            if (keyBandBottom < a.y || rowTop > b.y) continue;

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
                                               && !k.IsObjectReference
                                               && k.Binding.Equals(binding)
                                               && Mathf.Abs(k.Time - key.time) < 0.001f))
                        continue;

                    _selectedKeys.Add(new KeyframeRef
                        { Clip = clip, Binding = binding, Time = key.time, IsObjectReference = false });
                }
            }

            // Object-reference ключи (спрайты, материалы) внутри той же рамки выделения
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                if (keys == null) continue;

                foreach (var key in keys)
                {
                    if (key.time < tMin || key.time > tMax) continue;
                    if (_selectedKeys.Exists(k => ReferenceEquals(k.Clip, clip)
                                               && k.IsObjectReference
                                               && k.Binding.Equals(binding)
                                               && Mathf.Abs(k.Time - key.time) < 0.001f))
                        continue;

                    _selectedKeys.Add(new KeyframeRef
                        { Clip = clip, Binding = binding, Time = key.time, IsObjectReference = true });
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // KEYFRAME OPERATIONS
    // ═══════════════════════════════════════════════════

    private void MoveSelectedKeyframes(float deltaSeconds)
    {
        if (Mathf.Approximately(deltaSeconds, 0f)) return;

        // Группируем по (clip, binding) отдельно для float- и object-reference-кривых —
        // у них разное API в AnimationUtility, поэтому смешивать их в одном groups
        // нельзя (GetEditorCurve вернёт null для object-reference биндинга).
        var floatGroups  = new Dictionary<(AnimationClip, EditorCurveBinding), List<float>>();
        var objRefGroups = new Dictionary<(AnimationClip, EditorCurveBinding), List<float>>();

        foreach (var kref in _selectedKeys)
        {
            var groups = kref.IsObjectReference ? objRefGroups : floatGroups;
            var key = (kref.Clip, kref.Binding);
            if (!groups.ContainsKey(key)) groups[key] = new List<float>();
            groups[key].Add(kref.Time);
        }

        foreach (var (pair, times) in floatGroups)
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

        // Object-reference ключи (спрайты, материалы) — своя логика, это не AnimationCurve,
        // а обычный массив ObjectReferenceKeyframe, который нужно целиком пересобрать.
        foreach (var (pair, times) in objRefGroups)
        {
            var original = AnimationUtility.GetObjectReferenceCurve(pair.Item1, pair.Item2);
            if (original == null) continue;

            Undo.RecordObject(pair.Item1, "Move Keyframes");

            var list   = new List<ObjectReferenceKeyframe>(original);
            var toMove = new List<ObjectReferenceKeyframe>();

            foreach (float time in times)
            {
                int idx = FindObjRefKeyByTime(list.ToArray(), time);
                if (idx < 0) continue;
                toMove.Add(list[idx]);
                list.RemoveAt(idx);
            }

            foreach (var k in toMove)
            {
                var moved  = k;
                moved.time = Mathf.Max(0f, k.time + deltaSeconds);
                list.Add(moved);
            }

            list.Sort((a, b) => a.time.CompareTo(b.time));
            AnimationUtility.SetObjectReferenceCurve(pair.Item1, pair.Item2, list.ToArray());
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

    private int FindObjRefKeyByTime(ObjectReferenceKeyframe[] keys, float time, float tol = 0.001f)
    {
        for (int i = 0; i < keys.Length; i++)
            if (Mathf.Abs(keys[i].time - time) < tol) return i;
        return -1;
    }

    private void DeleteSelectedKeyframes()
    {
        if (_selectedKeys.Count == 0) return;

        // Разносим по float-кривым и object-reference-кривым (спрайты/материалы) —
        // у них разное API в AnimationUtility, поэтому группируем и чистим раздельно.
        var floatGroups = new Dictionary<(AnimationClip, EditorCurveBinding), List<float>>();
        var objGroups    = new Dictionary<(AnimationClip, EditorCurveBinding), List<float>>();

        foreach (var kref in _selectedKeys)
        {
            var dict = kref.IsObjectReference ? objGroups : floatGroups;
            var key  = (kref.Clip, kref.Binding);
            if (!dict.TryGetValue(key, out var list)) dict[key] = list = new List<float>();
            list.Add(kref.Time);
        }

        foreach (var (pair, times) in floatGroups)
        {
            var curve = AnimationUtility.GetEditorCurve(pair.Item1, pair.Item2);
            if (curve == null) continue;
            Undo.RecordObject(pair.Item1, "Delete Keyframes");

            // индексы удаляем с конца, чтобы предыдущие не сдвигались
            var indices = new List<int>();
            foreach (float t in times)
            {
                int idx = FindKeyByTime(curve, t);
                if (idx >= 0) indices.Add(idx);
            }
            indices.Sort((a, b) => b.CompareTo(a));
            foreach (int idx in indices) curve.RemoveKey(idx);

            AnimationUtility.SetEditorCurve(pair.Item1, pair.Item2, curve);
            EditorUtility.SetDirty(pair.Item1);
        }

        foreach (var (pair, times) in objGroups)
        {
            var keys = AnimationUtility.GetObjectReferenceCurve(pair.Item1, pair.Item2);
            if (keys == null) continue;
            Undo.RecordObject(pair.Item1, "Delete Keyframes");

            var kept = new List<ObjectReferenceKeyframe>(keys.Length);
            foreach (var k in keys)
            {
                bool remove = false;
                foreach (float t in times)
                    if (Mathf.Abs(k.time - t) < 0.001f) { remove = true; break; }
                if (!remove) kept.Add(k);
            }

            AnimationUtility.SetObjectReferenceCurve(pair.Item1, pair.Item2, kept.ToArray());
            EditorUtility.SetDirty(pair.Item1);
        }

        _selectedKeys.Clear();
        Repaint();
    }

    private void CopyKeyframes()
    {
        _clipboard.Clear();
        foreach (var kref in _selectedKeys)
        {
            if (!kref.IsObjectReference)
            {
                var curve = AnimationUtility.GetEditorCurve(kref.Clip, kref.Binding);
                if (curve == null) continue;
                int idx = FindKeyByTime(curve, kref.Time);
                if (idx < 0) continue;
                _clipboard.Add(new ClipEntry
                {
                    Binding = kref.Binding,
                    IsObjectReference = false,
                    Key  = curve.keys[idx],
                    Time = curve.keys[idx].time
                });
            }
            else
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(kref.Clip, kref.Binding);
                if (keys == null) continue;
                int idx = FindObjRefKeyByTime(keys, kref.Time);
                if (idx < 0) continue;
                _clipboard.Add(new ClipEntry
                {
                    Binding = kref.Binding,
                    IsObjectReference = true,
                    ObjectValue = keys[idx].value,
                    Time = keys[idx].time
                });
            }
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
        float origin = _clipboard[0].Time;

        // Object-reference вставки накапливаем по биндингу — SetObjectReferenceCurve
        // задаёт всю кривую целиком, поэтому не может вызываться по одному ключу за раз.
        var objRefGroups = new Dictionary<EditorCurveBinding, List<ObjectReferenceKeyframe>>();

        foreach (var entry in _clipboard)
        {
            float newTime = _currentTime + (entry.Time - origin);

            if (!entry.IsObjectReference)
            {
                var k = entry.Key;
                k.time = newTime;
                var curve = AnimationUtility.GetEditorCurve(target, entry.Binding)
                            ?? new AnimationCurve();
                curve.AddKey(k);
                AnimationUtility.SetEditorCurve(target, entry.Binding, curve);
            }
            else
            {
                if (!objRefGroups.TryGetValue(entry.Binding, out var list))
                {
                    var existing = AnimationUtility.GetObjectReferenceCurve(target, entry.Binding);
                    list = existing != null
                        ? new List<ObjectReferenceKeyframe>(existing)
                        : new List<ObjectReferenceKeyframe>();
                    objRefGroups[entry.Binding] = list;
                }
                list.Add(new ObjectReferenceKeyframe { time = newTime, value = entry.ObjectValue });
            }
        }

        foreach (var kv in objRefGroups)
        {
            kv.Value.Sort((a, b) => a.time.CompareTo(b.time));
            AnimationUtility.SetObjectReferenceCurve(target, kv.Key, kv.Value.ToArray());
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

                string relPath = AnimationUtility.CalculateTransformPath(comp.transform, animator.transform);
                var binding = new EditorCurveBinding
                {
                    path         = relPath,
                    type         = comp.GetType(),
                    propertyName = mod.currentValue.propertyPath
                };

                // КЛЮЧЕВОЙ МОМЕНТ: сама по себе запись значения в кривую клипа
                // не откатывает изменение на живом объекте. AnimationMode откатывает
                // при выходе из режима анимации только те свойства, для которых явно
                // зарегистрировано "исходное" (до правки) значение — вот этот вызов.
                // Без него правка, сделанная во время записи, так и остаётся висеть
                // на персонажe после ToggleRecording()/StopPreview(), хотя ключ уже
                // корректно лежит в клипе. Именно так это делает родное Animation Window.
                AnimationMode.AddPropertyModification(binding, mod.previousValue, false);

                // Object-reference свойства (спрайт, материал и т.п.) сериализуются
                // ИНАЧЕ, чем числовые: значение лежит в objectReference, а не в
                // строковом value (там пусто) — раньше это ловилось только веткой
                // float.TryParse, проваливалось и модификация тихо пропускалась.
                // Проверяем ОБЕ стороны (current/previous), чтобы не терять и случай
                // "спрайт очистили на None" (тогда currentValue.objectReference == null).
                bool isObjectReference = mod.currentValue.objectReference != null
                                          || mod.previousValue.objectReference != null;

                if (isObjectReference)
                {
                    Undo.RecordObject(part.clip, "Record Keyframe");
                    var existing = AnimationUtility.GetObjectReferenceCurve(part.clip, binding)
                                   ?? Array.Empty<ObjectReferenceKeyframe>();

                    var list = new List<ObjectReferenceKeyframe>(existing);
                    list.RemoveAll(k => Mathf.Approximately(k.time, _currentTime)); // не плодим дубли на том же кадре
                    list.Add(new ObjectReferenceKeyframe
                    {
                        time  = _currentTime,
                        value = mod.currentValue.objectReference
                    });
                    list.Sort((a, b) => a.time.CompareTo(b.time));

                    AnimationUtility.SetObjectReferenceCurve(part.clip, binding, list.ToArray());
                    EditorUtility.SetDirty(part.clip);
                    break;
                }

                if (!float.TryParse(mod.currentValue.value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float val)) break;

                Undo.RecordObject(part.clip, "Record Keyframe");
                var curve = AnimationUtility.GetEditorCurve(part.clip, binding)
                            ?? new AnimationCurve();

                // Убираем возможный существующий ключ на этом же кадре — иначе
                // AnimationCurve.AddKey может создать дубликат по времени.
                for (int k = curve.length - 1; k >= 0; k--)
                    if (Mathf.Approximately(curve[k].time, _currentTime))
                        curve.RemoveKey(k);

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

    [NonSerialized] private bool _draggingSplitter;
    private static readonly int SplitterHint = "MultiAnimatorSplitter".GetHashCode();

    private void DrawSplitterHandle()
    {
        Rect r = GUILayoutUtility.GetRect(SPLITTER_W, 1f,
            GUILayout.Width(SPLITTER_W), GUILayout.ExpandHeight(true));

        EditorGUI.DrawRect(r, _draggingSplitter
            ? new Color(0.35f, 0.55f, 0.85f)
            : new Color(0.08f, 0.08f, 0.08f));
        EditorGUIUtility.AddCursorRect(r, MouseCursor.ResizeHorizontal);

        // Стабильный ID по хэшу имени, а не по порядку вызовов — не важно,
        // сколько контролов Odin нарисует до нас в этом кадре.
        int controlId = GUIUtility.GetControlID(SplitterHint, FocusType.Passive, r);

        Event e = Event.current;

        if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
        {
            _draggingSplitter    = true;
            GUIUtility.hotControl = controlId;
            e.Use();
        }

        // КЛЮЧЕВОЕ МЕСТО: пока идёт драг, насильно отбираем hotControl обратно
        // КАЖДЫЙ кадр. Odin-виджеты (ValueDropdown, TableList, drag-and-drop
        // в object-полях) перехватывают hotControl под себя, стоит курсору
        // их задеть, — и без этой строки часть MouseDrag-событий достаётся
        // им, а не нам. Отсюда рывки крупными кусками вместо плавного
        // слежения за курсором: движение "копится", пока событие наконец
        // не долетит до нашего кода.
        if (_draggingSplitter && GUIUtility.hotControl != controlId)
            GUIUtility.hotControl = controlId;

        if (_draggingSplitter && e.type == EventType.MouseDrag)
        {
            float maxSidePanel = Mathf.Max(150f, position.width - MIN_TIMELINE_W - SPLITTER_W);
            _sidePanelWidth = Mathf.Clamp(_sidePanelWidth - e.delta.x, 150f, maxSidePanel);
            e.Use();
            Repaint();
        }

        // rawType — чтобы поймать отпускание, даже если e.type уже стал Used
        // из-за чужого e.Use() в этом же кадре.
        if (_draggingSplitter && e.rawType == EventType.MouseUp)
        {
            _draggingSplitter = false;
            if (GUIUtility.hotControl == controlId)
                GUIUtility.hotControl = 0;
            e.Use();
            Repaint();
        }
    }

    // ═══════════════════════════════════════════════════
    // SIDE PANEL
    // ═══════════════════════════════════════════════════

    private void DrawSidePanel()
    {
        if (_activeConfig == null) return;

        // Дропдаун стейта теперь только один — в тулбаре таймлайна (DrawStateSelectorBar).
        // Дублировать его тут больше не нужно, поэтому скроллим сразу таблицу партов/клипов
        // выбранного стейта на всю высоту панели.
        _sidePanelScroll = EditorGUILayout.BeginScrollView(
            _sidePanelScroll, GUILayout.ExpandHeight(true));

        // Редактор выбранного стейта через стандартный Editor
        // (Odin перехватывает Editor.CreateEditor → рисует с [TableList], [ValueDropdown] итд)
        if (_selectedState != null)
        {
            if (_stateEditor == null || _stateEditor.target != _selectedState)
            {
                if (_stateEditor != null) DestroyImmediate(_stateEditor);
                _stateEditor = UnityEditor.Editor.CreateEditor(_selectedState);
            }

            _stateEditor.OnInspectorGUI();
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Стейт не выбран — выбери его в выпадающем списке над таймлайном.",
                MessageType.Info);
        }

        EditorGUILayout.EndScrollView();
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

    private void DrawDiamond(Vector2 c, bool sel, Color baseColor)
    {
        float s   = sel ? DIAMOND + 1.5f : DIAMOND;
        Color col = sel ? new Color(1f, 0.82f, 0.15f) : baseColor;
        EditorGUI.DrawRect(new Rect(c.x - 1f, c.y - s,  2f, s), col);
        EditorGUI.DrawRect(new Rect(c.x - 1f, c.y,      2f, s), col);
        EditorGUI.DrawRect(new Rect(c.x - s,  c.y - 1f, s,  2f), col);
        EditorGUI.DrawRect(new Rect(c.x,       c.y - 1f, s,  2f), col);
    }

    // Заполненный квадрат — маркер object-reference ключа (смена спрайта/материала).
    // В нативном Animation-окне такие ключи тоже визуально отличаются от обычных
    // ромбов, чтобы сразу было видно "тут не интерполяция, а дискретная замена".
    private void DrawSpriteKeyMarker(Vector2 c, bool sel, Color baseColor)
    {
        float s   = sel ? DIAMOND + 1.5f : DIAMOND;
        Color col = sel ? new Color(1f, 0.82f, 0.15f) : baseColor;
        EditorGUI.DrawRect(new Rect(c.x - s, c.y - s, s * 2f, s * 2f), col);
    }

    // Маленькая точка в углу маркера — сигнал "тут сошлось больше одного
    // РАЗНОГО анимируемого свойства в один момент времени" (тот самый случай
    // слипания ключей). Не заменяет сам ромб/квадрат, а просто добавляется поверх.
    private void DrawStackedKeyBadge(Vector2 c)
    {
        var r = new Rect(c.x + 2f, c.y - DIAMOND - 5f, 4f, 4f);
        EditorGUI.DrawRect(r, new Color(1f, 0.85f, 0.2f));
    }

    private void DrawRectOutline(Rect r, Color col)
    {
        EditorGUI.DrawRect(new Rect(r.x,     r.y,     r.width, 1f), col);
        EditorGUI.DrawRect(new Rect(r.x,     r.yMax,  r.width, 1f), col);
        EditorGUI.DrawRect(new Rect(r.x,     r.y,     1f, r.height), col);
        EditorGUI.DrawRect(new Rect(r.xMax,  r.y,     1f, r.height), col);
    }
}