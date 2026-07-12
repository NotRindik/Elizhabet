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

    [PropertyOrder(1), OnInspectorGUI]
    private void DrawMainLayout()
    {
        if (_activeTag == null) return;

        EditorGUILayout.BeginHorizontal();

        // ── Таймлайн (левая, растягивается) ──────────
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        DrawTimelineSection();
        GUILayout.EndVertical();

        // ── Сплиттер ─────────────────────────────────
        DrawSplitterHandle();

        // ── Боковая панель (правая, фиксированная) ───
        GUILayout.BeginVertical(GUILayout.Width(_sidePanelWidth));
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