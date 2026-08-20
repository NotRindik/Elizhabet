// Editor/MultiAnimatorEditorWindow.cs

using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

public partial class MultiAnimatorEditorWindow : OdinEditorWindow
{
    [MenuItem("Window/Multi Animator Editor")]
    public static void Open() => GetWindow<MultiAnimatorEditorWindow>("Multi Animator");

    // ═══════════════════════════════════════════════════
    // ACTIVE STATE
    // ═══════════════════════════════════════════════════

    [NonSerialized] private AnimationComposerTag    _activeTag;
    [NonSerialized] private AnimationComposerConfig _activeConfig;
    [NonSerialized] private GameObject              _activeRoot;

    // ═══════════════════════════════════════════════════
    // INTERNAL
    // ═══════════════════════════════════════════════════

    [NonSerialized] private Dictionary<string, Animator> _animatorByPart = new();
    [NonSerialized] private int    _selectedIndex = -1;
    [NonSerialized] private bool   _isPlaying;
    [NonSerialized] private double _lastTick;
    [NonSerialized] private float  _currentTime;  // секунды
    [NonSerialized] private AnimationStateConfig _selectedState;

    // ═══════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════

    protected override void OnEnable()
    {
        base.OnEnable();
        Selection.selectionChanged += OnSelectionChanged;
        EditorApplication.update   += Tick;
        OnSelectionChanged();
        OnWindowOpened?.Invoke();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Selection.selectionChanged -= OnSelectionChanged;
        EditorApplication.update   -= Tick;
        if (_isRecording) ToggleRecording();
        StopPreview();
        if (_stateEditor != null) DestroyImmediate(_stateEditor);
        OnWindowClosed?.Invoke();
    }

    // ═══════════════════════════════════════════════════
    // SELECTION
    // ═══════════════════════════════════════════════════

    private void OnSelectionChanged()
    {
        var go  = Selection.activeGameObject;
        var tag = go != null
            ? go.GetComponent<AnimationComposerTag>()
              ?? go.GetComponentInParent<AnimationComposerTag>()
            : null;

        if (tag == _activeTag) return;

        StopPreview();
        _selectedState = null;
        _selectedIndex = -1;
        _activeTag     = tag;

        if (_activeTag != null)
        {
            _activeRoot   = _activeTag.gameObject;
            _activeConfig = _activeTag.config;
            RefreshAnimators();

            // Автовыбор первого стейта по счёту, если он есть — раньше при
            // открытии окна с уже настроенным персонажем приходилось лишний
            // раз лезть в дропдаун, чтобы просто начать работать.
            if (_activeConfig != null && _activeConfig.states != null && _activeConfig.states.Count > 0)
            {
                _selectedIndex = 0;
                _selectedState = _activeConfig.states[0];
                _stateEditor   = null;
                OnStateSelected?.Invoke(_selectedState);
            }
        }
        else
        {
            _activeRoot   = null;
            _activeConfig = null;
            _animatorByPart.Clear();
            AnimatorRegistry.Animators.Clear();
        }
        OnRootChanged?.Invoke(_activeTag);
        Repaint();
    }

    // ═══════════════════════════════════════════════════
    // HOTKEYS  [Order -1 — раньше заголовка и всего остального]
    // ═══════════════════════════════════════════════════

    [PropertyOrder(-1), OnInspectorGUI]
    private void HandleGlobalHotkeys()
    {
        if (_activeTag == null) return;

        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;

        bool ctrlOrCmd = e.control || e.command; // command — чтобы работало и на маке

        // Ctrl/Cmd+S работает даже во время ввода текста (имя стейта и т.п.) —
        // это стандартное поведение "Save" в любом редакторе.
        if (ctrlOrCmd && e.keyCode == KeyCode.S)
        {
            SaveAll();
            e.Use();
            return;
        }

        // Остальные хоткеи глушим во время печати в текстовое поле — иначе
        // Space вставит пробел в имя стейта вместо Play/Pause, C/V потрут
        // системный copy/paste текста и т.д.
        if (EditorGUIUtility.editingTextField) return;

        switch (e.keyCode)
        {
            case KeyCode.Space:
                if (_isPlaying) StopPreview(); else StartPreview();
                e.Use();
                break;

            case KeyCode.Home:
                SetTime(0f);
                e.Use();
                break;

            case KeyCode.End:
                SetTime(GetMaxClipLength());
                e.Use();
                break;

            // Стрелки без модификатора — покадровый шаг.
            // Alt+стрелка — прыжок к предыдущему/следующему существующему ключу.
            //
            // Раньше шаг был захардкожен как "1f / 30f" — фиксированные 30 fps,
            // независимо от реального fps клипов. Если у стейта клипы на 24 или
            // 60 fps (а разные парты вполне могут иметь разный fps — см.
            // ReferenceFrameRate в Timeline-файле), шаг либо не долетал до
            // следующего реального кадра, либо перепрыгивал через него.
            // ReferenceFrameRate — это уже посчитанный максимальный fps среди
            // клипов текущего стейта, тот же самый, по которому строится
            // формат "секунды:кадры" в тулбаре — так что стрелки теперь всегда
            // попадают ровно на границы кадров, которые показывает таймлайн.
            case KeyCode.LeftArrow:
                if (e.alt) StepToPrevKey();
                else SetTime(_currentTime - 1f / ReferenceFrameRate);
                e.Use();
                break;

            case KeyCode.RightArrow:
                if (e.alt) StepToNextKey();
                else SetTime(_currentTime + 1f / ReferenceFrameRate);
                e.Use();
                break;

            case KeyCode.R:
                ToggleRecording();
                e.Use();
                break;

            case KeyCode.C:
                if (ctrlOrCmd && _selectedKeys.Count > 0) { CopyKeyframes(); e.Use(); }
                break;

            case KeyCode.V:
                if (ctrlOrCmd && _clipboard.Count > 0) { PasteKeyframes(); e.Use(); }
                break;

            case KeyCode.Delete:
            case KeyCode.Backspace:
                if (_selectedKeys.Count > 0) { DeleteSelectedKeyframes(); e.Use(); }
                break;
        }
    }

    private void SaveAll()
    {
        if (_activeConfig != null)  EditorUtility.SetDirty(_activeConfig);
        if (_selectedState != null)
        {
            EditorUtility.SetDirty(_selectedState);
            foreach (var p in _selectedState.parts)
                if (p.clip != null) EditorUtility.SetDirty(p.clip);
        }
        AssetDatabase.SaveAssets();
        ShowNotification(new GUIContent("Сохранено"));
    }

    // ═══════════════════════════════════════════════════
    // HEADER  [Order 0]
    // ═══════════════════════════════════════════════════

    [PropertyOrder(0), OnInspectorGUI]
    private void DrawHeader()
    {
        EditorGUILayout.Space(4);

        if (_activeTag == null)
        {
            SirenixEditorGUI.InfoMessageBox(
                "Выбери объект с компонентом AnimationComposerTag на сцене или среди префабов");
            return;
        }

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("🎬", GUILayout.Width(20), GUILayout.Height(18));
        GUILayout.Label(_activeRoot.name, EditorStyles.boldLabel);
        GUILayout.Label("→", GUILayout.Width(16));

        if (_activeConfig != null)
            GUILayout.Label(_activeConfig.name, EditorStyles.miniLabel);
        else
        {
            GUIHelper.PushColor(new Color(1f, 0.6f, 0.3f));
            GUILayout.Label("Config не назначен!", EditorStyles.miniLabel);
            GUIHelper.PopColor();
        }

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Обновить аниматоры", EditorStyles.toolbarButton, GUILayout.Width(150)))
            RefreshAnimators();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);
    }

    // ═══════════════════════════════════════════════════
    // MAIN LAYOUT  [Order 1]
    // Таймлайн слева | сплиттер | боковая панель справа
    // ═══════════════════════════════════════════════════

    private const float MIN_TIMELINE_W = 220f;

    [PropertyOrder(1), OnInspectorGUI]
    private void DrawMainLayout()
    {
        if (_activeTag == null) return;

        // Не даём _sidePanelWidth требовать больше места, чем физически есть в окне.
        // Если сумма (таймлайн-минимум + сплиттер + панель) превышает position.width,
        // GUILayout не сжимает ExpandWidth-колонку аккуратно — она может уехать почти
        // в ноль/за видимый край, и сплиттер визуально "залипает", пока перетаскивание
        // не приведёт сумму обратно к вмещающейся ширине. Клампим на входе, чтобы
        // такая раскладка вообще не могла возникнуть.
        float maxSidePanel = Mathf.Max(150f, position.width - MIN_TIMELINE_W - SPLITTER_W);
        if (_sidePanelWidth > maxSidePanel)
            _sidePanelWidth = maxSidePanel;

        // ExpandHeight на всём ряду и на обеих колонках — иначе GUILayout сайзит
        // каждую колонку строго под собственный контент, и остаток окна снизу
        // остаётся пустым/незакрашенным (та самая "дыра" под таймлайном).
        EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));

        // ── Таймлайн (левая, растягивается по ширине и высоте) ──
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        DrawTimelineSection();
        GUILayout.EndVertical();

        // ── Сплиттер ─────────────────────────────────
        DrawSplitterHandle();

        // ── Боковая панель (правая, фиксированная ширина, растягивается по высоте) ──
        GUILayout.BeginVertical(GUILayout.Width(_sidePanelWidth), GUILayout.ExpandHeight(true));
        DrawSidePanel();
        GUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    // ═══════════════════════════════════════════════════
    // WINDOW EVENTS (подписка из внешних модулей)
    // ═══════════════════════════════════════════════════

    public static event Action                        OnWindowOpened;
    public static event Action                        OnWindowClosed;
    public static event Action<AnimationComposerTag>  OnRootChanged;
    public static event Action<AnimationStateConfig>  OnStateSelected;
    public static event Action<AnimationStateConfig>  OnStateCreated;
    public static event Action<AnimationStateConfig>  OnStateRemoved;
    public static event Action                        OnPlay;
    public static event Action                        OnPause;
    public static event Action                        OnStop;
    public static event Action<float>                 OnTimeChanged;    // секунды
    public static event Action<bool>                  OnRecordChanged;  // true = запись включена

    // ═══════════════════════════════════════════════════
    // PREVIEW LOGIC
    // ═══════════════════════════════════════════════════

    private void Tick()
    {
        if (!_isPlaying || _selectedState == null) return;
        float delta  = (float)(EditorApplication.timeSinceStartup - _lastTick);
        _lastTick    = EditorApplication.timeSinceStartup;
        float maxLen = GetMaxClipLength();
        _currentTime = Mathf.Repeat(_currentTime + delta, maxLen);
        SampleAll(_currentTime);
        Repaint();
    }

    internal void StartPreview()
    {
        if (_selectedState == null || _activeRoot == null) return;
        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
        bool wasPaused = !_isPlaying;
        _isPlaying = true;
        _lastTick  = EditorApplication.timeSinceStartup;
        if (wasPaused) OnPlay?.Invoke();
    }

    internal void StopPreview()
    {
        bool wasPlaying = _isPlaying;
        _isPlaying = false;
        if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
        if (wasPlaying) OnPause?.Invoke();
    }

    internal void StopAndReset()
    {
        bool wasPlaying = _isPlaying;
        _isPlaying = false;
        if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
        SetTime(0f);
        if (wasPlaying) OnStop?.Invoke();
    }

    internal void SampleAll(float t)
    {
        if (_selectedState == null || _activeRoot == null) return;
        if (!AnimationMode.InAnimationMode()) return;
        foreach (var part in _selectedState.parts)
        {
            if (part.clip == null) continue;
            if (!_animatorByPart.TryGetValue(part.partName, out var anim)) continue;
            AnimationMode.SampleAnimationClip(anim.gameObject, part.clip, t);
        }
    }

    internal void SetTime(float t)
    {
        _currentTime = Mathf.Clamp(t, 0f, GetMaxClipLength());
        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
        SampleAll(_currentTime);
        OnTimeChanged?.Invoke(_currentTime);
        Repaint();
    }

    internal float GetMaxClipLength()
    {
        if (_selectedState == null) return 1f;
        float max = 0f;
        foreach (var p in _selectedState.parts)
            if (p.clip != null) max = Mathf.Max(max, p.clip.length);
        return max > 0 ? max : 1f;
    }

    // ═══════════════════════════════════════════════════
    // ANIMATORS
    // ═══════════════════════════════════════════════════

    private void RefreshAnimators()
    {
        _animatorByPart.Clear();
        AnimatorRegistry.Animators.Clear();
        if (_activeRoot == null) return;
        foreach (var a in _activeRoot.GetComponentsInChildren<Animator>(true))
        {
            _animatorByPart[a.gameObject.name]            = a;
            AnimatorRegistry.Animators[a.gameObject.name] = a;
        }
        Repaint();
    }

    // ═══════════════════════════════════════════════════
    // ASSET MANAGEMENT
    // ═══════════════════════════════════════════════════

    private void AddState()
    {
        if (_activeConfig == null) return;
        Undo.RecordObject(_activeConfig, "Add State");

        var s = CreateInstance<AnimationStateConfig>();
        s.name = s.stateName = "NewState";
        s.parts = new();
        AssetDatabase.AddObjectToAsset(s, _activeConfig);
        _activeConfig.states.Add(s);
        EditorUtility.SetDirty(_activeConfig);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        _selectedIndex = _activeConfig.states.Count - 1;
        _selectedState = s;
        _stateEditor   = null;
        OnStateCreated?.Invoke(s);
        OnStateSelected?.Invoke(s);
    }

    private void RemoveSelectedState()
    {
        if (_selectedState == null || _activeConfig == null) return;
        StopPreview();
        Undo.RecordObject(_activeConfig, "Remove State");

        var removed = _selectedState;
        _activeConfig.states.RemoveAt(_selectedIndex);
        AssetDatabase.RemoveObjectFromAsset(removed);
        EditorUtility.SetDirty(_activeConfig);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        _selectedState = null;
        _selectedIndex = -1;
        _stateEditor   = null;
        OnStateRemoved?.Invoke(removed);
    }
}