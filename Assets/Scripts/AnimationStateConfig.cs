using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif


public class AnimationStateConfig : ScriptableObject
{
    [HideLabel, Title("@stateName", bold: false)]
    [DelayedProperty, OnValueChanged("SyncName")]
    public string stateName;

    [TableList(
        AlwaysExpanded      = true,
        ShowIndexLabels     = false,
        HideToolbar         = false,
        DrawScrollView      = true,
        MinScrollViewHeight = 80,
        ScrollViewHeight    = 220)]
    public List<PartEntry> parts = new();

#if UNITY_EDITOR

    [OnInspectorGUI, PropertyOrder(-100)]
    private void EnsurePartOwners()
    {
        if (parts == null) return;
        foreach (var p in parts)
            p.Owner = this;
    }
#endif

    [Serializable]
    public class PartEntry
    {
        [TableColumnWidth(150, Resizable = true)] [ValueDropdown("GetPartNames"), HideLabel]
        public string partName;

        [TableColumnWidth(200, Resizable = true)]
        [ValueDropdown("GetClips"), HideLabel]
#if UNITY_EDITOR
        [OnValueChanged("OnClipFieldChanged")]
#endif
        public AnimationClip clip;

        [SerializeField] private string _animatorStateAlias;

        public string AnimatorStateAlias
        {
            get =>     string.IsNullOrEmpty(_animatorStateAlias) ? clip != null ? clip.name : null : _animatorStateAlias;
            set => _animatorStateAlias = value;
        }

#if UNITY_EDITOR
        [NonSerialized] internal AnimationStateConfig Owner;
        [NonSerialized] private AnimationClip _createSentinel;
        [NonSerialized] private AnimationClip _deleteSentinel;
        [NonSerialized] private AnimationClip _deleteSentinelTarget;
        [NonSerialized] private AnimationClip _lastRealClip;

        private IEnumerable<string> GetPartNames() =>
            AnimatorRegistry.Animators.Keys;

        private IEnumerable<AnimationClip> GetClips()
        {
            var result = new List<AnimationClip>();

            if (string.IsNullOrEmpty(partName)
                || !AnimatorRegistry.Animators.TryGetValue(partName, out var animator))
                return result;

            _createSentinel ??= MakeSentinel("＋ Создать новый клип...");
            result.Add(_createSentinel);
            
            if (clip != null && !ReferenceEquals(clip, _createSentinel) && !ReferenceEquals(clip, _deleteSentinel))
                _lastRealClip = clip;

            if (_lastRealClip != null)
            {
                if (_deleteSentinel == null || !ReferenceEquals(_deleteSentinelTarget, _lastRealClip))
                {
                    _deleteSentinel       = MakeSentinel($"🗑 Удалить «{_lastRealClip.name}»");
                    _deleteSentinelTarget = _lastRealClip;
                }
                result.Add(_deleteSentinel);
            }

            var ctrl = animator.runtimeAnimatorController;
            if (ctrl != null) result.AddRange(ctrl.animationClips);

            return result;
        }

        private static AnimationClip MakeSentinel(string label) => new() { name = label };

        private void OnClipFieldChanged()
        {
            if (ReferenceEquals(clip, _createSentinel))
            {
                clip = _lastRealClip;
                AnimationClipEditorActions.CreateNewClip(this);
            }
            else if (ReferenceEquals(clip, _deleteSentinel))
            {
                var target = _lastRealClip;
                clip = null;
                AnimationClipEditorActions.DeleteClip(this, target);
            }
        }
#endif
    }

#if UNITY_EDITOR
    private void SyncName()
    {
        name = stateName;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();
    }
#endif
}

#if UNITY_EDITOR
internal static class AnimationClipEditorActions
{
    private static string LastSaveDirPrefKey =>
        "MultiAnimator.LastClipSaveDir_" + Application.dataPath.GetHashCode();

    private static string LastSaveDir
    {
        get => EditorPrefs.GetString(LastSaveDirPrefKey, null);
        set => EditorPrefs.SetString(LastSaveDirPrefKey, value);
    }

    public static void CreateNewClip(AnimationStateConfig.PartEntry entry)
    {
        if (string.IsNullOrEmpty(entry.partName)
            || !AnimatorRegistry.Animators.TryGetValue(entry.partName, out var animator))
        {
            EditorUtility.DisplayDialog("Не выбран парт",
                "Сначала выбери Part Name — новый клип привязывается к его Animator Controller'у.", "Ок");
            return;
        }

        var controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Не поддерживается",
                $"У «{entry.partName}» либо не назначен AnimatorController, либо это Override Controller — " +
                "новый клип создать некуда. Назначь обычный AnimatorController на Animator этого объекта.",
                "Ок");
            return;
        }
        
        string cached     = LastSaveDir;
        string defaultDir = !string.IsNullOrEmpty(cached) && AssetDatabase.IsValidFolder(cached)
            ? cached
            : GetActiveProjectFolder();

        string path = EditorUtility.SaveFilePanelInProject(
            "Создать анимационный клип", "New Animation", "anim",
            "Куда сохранить новый клип?", defaultDir);
        if (string.IsNullOrEmpty(path))
            return;

        var newClip = new AnimationClip();
        AssetDatabase.CreateAsset(newClip, path);
        
        LastSaveDir = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        
        string stateName = System.IO.Path.GetFileNameWithoutExtension(path);
        var    sm        = controller.layers[0].stateMachine;
        var    state     = sm.AddState(stateName);
        state.motion = newClip;

        EditorUtility.SetDirty(controller);
        if (entry.Owner != null) EditorUtility.SetDirty(entry.Owner);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        entry.clip = newClip;
    }

    public static void DeleteClip(AnimationStateConfig.PartEntry entry, AnimationClip target)
    {
        if (target == null) return;

        bool confirmed = EditorUtility.DisplayDialog(
            "Удалить клип?",
            $"Удалить файл клипа «{target.name}» насовсем?\n\n" +
            "Это действие нельзя отменить, и клип пропадёт из всех стейтов " +
            "(во всех AnimationStateConfig'ах), где он использовался.",
            "Удалить", "Отмена");
        if (!confirmed) return;
        
        if (!string.IsNullOrEmpty(entry.partName)
            && AnimatorRegistry.Animators.TryGetValue(entry.partName, out var animator)
            && animator.runtimeAnimatorController is AnimatorController controller)
        {
            var sm = controller.layers[0].stateMachine;
            foreach (var child in sm.states)
            {
                if (child.state.motion == target)
                {
                    sm.RemoveState(child.state);
                    break;
                }
            }
            EditorUtility.SetDirty(controller);
        }

        if (entry.Owner != null) EditorUtility.SetDirty(entry.Owner);

        string assetPath = AssetDatabase.GetAssetPath(target);
        if (!string.IsNullOrEmpty(assetPath))
            AssetDatabase.DeleteAsset(assetPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static string GetActiveProjectFolder()
    {
        foreach (var obj in Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Assets))
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;
            return System.IO.File.Exists(path) ? System.IO.Path.GetDirectoryName(path) : path;
        }
        return "Assets";
    }
}
#endif