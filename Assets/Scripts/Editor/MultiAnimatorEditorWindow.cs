// Editor/MultiAnimatorEditorWindow.cs

using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

public class MultiAnimatorEditorWindow : OdinEditorWindow
{
    [MenuItem("Window/Multi Animator Editor")]
    public static void Open() => GetWindow<MultiAnimatorEditorWindow>("Multi Animator");

    // ═══════════════════════════════════════════════════
    // ACTIVE STATE (из Selection, не из поля)
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

    // ═══════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════

    protected override void OnEnable()
    {
        base.OnEnable();
        Selection.selectionChanged   += OnSelectionChanged;
        EditorApplication.update     += Tick;
        OnSelectionChanged();              // инициализация при открытии окна
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Selection.selectionChanged   -= OnSelectionChanged;
        EditorApplication.update     -= Tick;
        StopPreview();
    }

    // ═══════════════════════════════════════════════════
    // SELECTION HANDLER
    // ═══════════════════════════════════════════════════

    private void OnSelectionChanged()
    {
        var go  = Selection.activeGameObject;
        var tag = go != null
            ? go.GetComponent<AnimationComposerTag>()
              ?? go.GetComponentInParent<AnimationComposerTag>()
            : null;

        // тот же объект — не перестраиваем
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

        Repaint();
    }

    // ═══════════════════════════════════════════════════
    // HEADER — рисуется всегда первым
    // ═══════════════════════════════════════════════════

    [OnInspectorGUI]
    private void DrawHeader()
    {
        EditorGUILayout.Space(4);

        if (_activeTag == null)
        {
            SirenixEditorGUI.InfoMessageBox(
                "Выбери объект с компонентом AnimationComposerTag на сцене или среди префабов");
            return;
        }

        // Строка с именем объекта и конфига
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label("\ud83c\udfac", GUILayout.Width(20), GUILayout.Height(18));
        GUILayout.Label(_activeRoot.name,  EditorStyles.boldLabel);
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
    // ЛЕВАЯ ПАНЕЛЬ — список стейтов
    // Объявлен ДО _selectedState → рисуется левее в HorizontalGroup
    // ═══════════════════════════════════════════════════

    [HorizontalGroup("Editor", Width = 200, MarginRight = 6)]
    [OnInspectorGUI]
    private void DrawStateList()
    {
        if (_activeConfig == null) return;

        SirenixEditorGUI.Title("Стейты", null, TextAlignment.Left, horizontalLine: true);

        var states = _activeConfig.states;
        for (int i = 0; i < states.Count; i++)
        {
            var s = states[i];
            if (s == null) continue;

            bool sel = i == _selectedIndex;
            if (sel) GUIHelper.PushColor(new Color(0.4f, 0.82f, 1f));

            if (GUILayout.Button(s.stateName, GUILayout.Height(26)))
            {
                _selectedIndex = i;
                _selectedState = s;
            }

            if (sel) GUIHelper.PopColor();
        }

        GUILayout.Space(6);

        if (GUILayout.Button(
                new GUIContent("  + Добавить стейт", EditorIcons.Plus.Active),
                EditorStyles.miniButton, GUILayout.Height(22)))
            AddState();

        if (_selectedState != null)
        {
            GUIHelper.PushColor(new Color(1f, 0.38f, 0.38f));
            if (GUILayout.Button(
                    new GUIContent("  − Удалить", EditorIcons.X.Active),
                    EditorStyles.miniButton, GUILayout.Height(22)))
                RemoveSelectedState();
            GUIHelper.PopColor();
        }
    }

    // ═══════════════════════════════════════════════════
    // ПРАВАЯ ПАНЕЛЬ — редактор выбранного стейта
    // ═══════════════════════════════════════════════════

    [HorizontalGroup("Editor")]
    [ShowInInspector, NonSerialized]
    [InlineEditor(
        InlineEditorObjectFieldModes.Hidden,
        Expanded    = true,
        DrawGUI     = true,
        DrawPreview = false)]
    [HideLabel]
    private AnimationStateConfig _selectedState;

    // ═══════════════════════════════════════════════════
    // PREVIEW
    // ═══════════════════════════════════════════════════

    [BoxGroup("Preview")]
    [HorizontalGroup("Preview/Controls", Width = 120)]
    [Button("@_isPlaying ? \"⏸  Пауза\" : \"▶  Play\"", ButtonSizes.Small)]
    [GUIColor("@_isPlaying ? new Color(1f,0.85f,0.3f) : new Color(0.45f,1f,0.55f)")]
    private void TogglePlay()
    {
        if (_isPlaying) StopPreview();
        else            StartPreview();
    }

    [HorizontalGroup("Preview/Controls", Width = 80)]
    [Button("■  Стоп", ButtonSizes.Small)]
    private void StopAndReset()
    {
        StopPreview();
        _currentTime = 0f;
        SampleAll(0f);
    }

    [BoxGroup("Preview")]
    [ShowInInspector, NonSerialized]
    [PropertyRange(0f, 1f), OnValueChanged("OnTimeScrub")]
    [LabelText("Время")]
    private float _currentTime;

    // ═══════════════════════════════════════════════════
    // PREVIEW LOGIC
    // ═══════════════════════════════════════════════════

    private void Tick()
    {
        if (!_isPlaying || _selectedState == null) return;
        float delta  = (float)(EditorApplication.timeSinceStartup - _lastTick);
        _lastTick    = EditorApplication.timeSinceStartup;
        _currentTime = Mathf.Repeat(_currentTime + delta, 1f);
        SampleAll(_currentTime);
        Repaint();
    }

    private void OnTimeScrub()
    {
        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
        SampleAll(_currentTime);
    }

    private void StartPreview()
    {
        if (_selectedState == null || _activeRoot == null) return;
        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
        _isPlaying = true;
        _lastTick  = EditorApplication.timeSinceStartup;
    }

    private void StopPreview()
    {
        _isPlaying = false;
        if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
    }

    private void SampleAll(float normalizedTime)
    {
        if (_selectedState == null || _activeRoot == null) return;
        if (!AnimationMode.InAnimationMode()) return;

        foreach (var part in _selectedState.parts)
        {
            if (part.clip == null) continue;
            if (!_animatorByPart.TryGetValue(part.partName, out var animator)) continue;
            AnimationMode.SampleAnimationClip(
                animator.gameObject,
                part.clip,
                normalizedTime * part.clip.length);
        }
    }

    // ═══════════════════════════════════════════════════
    // ANIMATORS
    // ═══════════════════════════════════════════════════

    private void RefreshAnimators()
    {
        _animatorByPart.Clear();
        AnimatorRegistry.Animators.Clear();
        if (_activeRoot == null) return;

        foreach (var a in _activeRoot.GetComponentsInChildren<Animator>(includeInactive: true))
        {
            var key = a.gameObject.name;
            _animatorByPart[key]            = a;
            AnimatorRegistry.Animators[key] = a;
        }

        Repaint();
    }

    // ═══════════════════════════════════════════════════
    // ASSET MANAGEMENT
    // ═══════════════════════════════════════════════════

    private void AddState()
    {
        if (_activeConfig == null) return;
        Undo.RecordObject(_activeConfig, "Add Animation State");

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
    }

    private void RemoveSelectedState()
    {
        if (_selectedState == null || _activeConfig == null) return;
        StopPreview();
        Undo.RecordObject(_activeConfig, "Remove Animation State");

        _activeConfig.states.RemoveAt(_selectedIndex);
        AssetDatabase.RemoveObjectFromAsset(_selectedState);
        EditorUtility.SetDirty(_activeConfig);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        _selectedState = null;
        _selectedIndex = -1;
    }
}