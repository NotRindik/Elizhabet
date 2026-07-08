using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

// Subasset — создаётся только через MultiAnimatorEditorWindow
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

    // ── PartEntry: класс (не struct) — нужен для ValueDropdown instance-методов ──
    [Serializable]
    public class PartEntry
    {
        // Dropdown из аниматоров зарегистрированных окном
        [TableColumnWidth(150, Resizable = true)]
        [ValueDropdown("GetPartNames"), HideLabel]
        public string partName;

        // Dropdown из клипов аниматора выбранного выше
        [TableColumnWidth(200, Resizable = true)]
        [ValueDropdown("GetClips"), HideLabel]
        public AnimationClip clip;

#if UNITY_EDITOR
        private IEnumerable<string> GetPartNames() =>
            AnimatorRegistry.Animators.Keys;

        private IEnumerable<AnimationClip> GetClips()
        {
            if (string.IsNullOrEmpty(partName))
                return Enumerable.Empty<AnimationClip>();
            if (!AnimatorRegistry.Animators.TryGetValue(partName, out var animator))
                return Enumerable.Empty<AnimationClip>();
            var ctrl = animator.runtimeAnimatorController;
            return ctrl != null ? ctrl.animationClips : Enumerable.Empty<AnimationClip>();
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