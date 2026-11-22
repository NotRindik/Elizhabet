using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using PartEntry = AnimationStateConfig.PartEntry;

namespace MultiAnimator2
{
    public static class StateAssets
    {
        private static string LastDirKey => "MultiAnimator2.LastClipDir_" + Application.dataPath.GetHashCode();

        public static AnimationStateConfig AddState(AnimationComposerConfig config, string baseName,
            AnimationStateConfig source = null)
        {
            if (config == null)
                return null;

            Undo.RecordObject(config, "Add State");
            var state = ScriptableObject.CreateInstance<AnimationStateConfig>();
            state.stateName = state.name = UniqueName(config, baseName);
            state.parts = new List<PartEntry>();

            if (source != null && source.parts != null)
                foreach (var p in source.parts)
                    state.parts.Add(CopyPart(p));

            AssetDatabase.AddObjectToAsset(state, config);
            config.states ??= new List<AnimationStateConfig>();
            config.states.Add(state);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return state;
        }

        public static void RemoveState(AnimationComposerConfig config, AnimationStateConfig state)
        {
            if (config == null || state == null)
                return;

            Undo.RecordObject(config, "Remove State");
            config.states.Remove(state);
            if (config.layers != null)
                foreach (var layer in config.layers)
                    layer.states?.Remove(state);

            AssetDatabase.RemoveObjectFromAsset(state);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        public static void RenameState(AnimationComposerConfig config, AnimationStateConfig state, string newName)
        {
            if (state == null || string.IsNullOrWhiteSpace(newName) || newName == state.stateName)
                return;

            Undo.RecordObject(state, "Rename State");
            state.stateName = newName;
            state.name = newName;
            EditorUtility.SetDirty(state);
            if (config != null)
                EditorUtility.SetDirty(config);
        }

        public static void AddPart(AnimationStateConfig state, string partName)
        {
            Undo.RecordObject(state, "Add Part");
            state.parts ??= new List<PartEntry>();
            state.parts.Add(new PartEntry { partName = partName });
            EditorUtility.SetDirty(state);
        }

        public static void RemovePart(AnimationStateConfig state, int index)
        {
            if (state.parts == null || index < 0 || index >= state.parts.Count)
                return;
            Undo.RecordObject(state, "Remove Part");
            state.parts.RemoveAt(index);
            EditorUtility.SetDirty(state);
        }

        public static void SetPartName(MultiAnimatorSession session, AnimationStateConfig state, int index,
            string partName)
        {
            var part = state.parts[index];
            Undo.RecordObject(state, "Change Part");
            part.partName = partName;
            SyncAlias(session, part);
            EditorUtility.SetDirty(state);
        }

        public static void SetPartClip(MultiAnimatorSession session, AnimationStateConfig state, int index,
            AnimationClip clip)
        {
            var part = state.parts[index];
            Undo.RecordObject(state, "Change Clip");
            part.clip = clip;
            part.AnimatorStateAlias = null;
            SyncAlias(session, part);
            EditorUtility.SetDirty(state);
        }

        public static IEnumerable<AnimationClip> ControllerClips(Animator animator)
        {
            var controller = animator != null ? animator.runtimeAnimatorController : null;
            if (controller == null)
                return Enumerable.Empty<AnimationClip>();
            return controller.animationClips.Where(c => c != null).Distinct().OrderBy(c => c.name);
        }

        public static AnimationClip CreateClip(MultiAnimatorSession session, AnimationStateConfig state, int index)
        {
            var part = state.parts[index];
            if (!session.TryGetAnimator(part, out var animator))
            {
                EditorUtility.DisplayDialog("Нет части", "Сначала выбери часть: клип привязывается к её Animator Controller.", "Ок");
                return null;
            }

            if (animator.runtimeAnimatorController is not AnimatorController controller)
            {
                EditorUtility.DisplayDialog("Не поддерживается",
                    $"У «{part.partName}» нет обычного AnimatorController (пусто или Override Controller).", "Ок");
                return null;
            }

            var lastDir = EditorPrefs.GetString(LastDirKey, "");
            var dir = !string.IsNullOrEmpty(lastDir) && AssetDatabase.IsValidFolder(lastDir) ? lastDir : "Assets";
            var path = EditorUtility.SaveFilePanelInProject("Новый клип", $"{state.stateName}_{part.partName}",
                "anim", "Куда сохранить клип?", dir);
            if (string.IsNullOrEmpty(path))
                return null;

            var clip = new AnimationClip { frameRate = session.FrameRate };
            AssetDatabase.CreateAsset(clip, path);
            EditorPrefs.SetString(LastDirKey, Path.GetDirectoryName(path)?.Replace('\\', '/'));

            var animatorState = controller.layers[0].stateMachine.AddState(Path.GetFileNameWithoutExtension(path));
            animatorState.motion = clip;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            SetPartClip(session, state, index, clip);
            return clip;
        }

        public static void DeleteClip(MultiAnimatorSession session, AnimationStateConfig state, int index)
        {
            var part = state.parts[index];
            var clip = part.clip;
            if (clip == null)
                return;

            if (!EditorUtility.DisplayDialog("Удалить клип?",
                    $"Удалить файл «{clip.name}» насовсем? Это не отменить через Undo. Клип пропадёт из всех стейтов этого конфига.",
                    "Удалить", "Отмена"))
                return;

            if (session.TryGetAnimator(part, out var animator) &&
                animator.runtimeAnimatorController is AnimatorController controller)
            {
                var sm = controller.layers[0].stateMachine;
                foreach (var child in sm.states)
                {
                    if (child.state.motion != clip)
                        continue;
                    sm.RemoveState(child.state);
                    break;
                }
                EditorUtility.SetDirty(controller);
            }

            var config = session.Config;
            if (config != null && config.states != null)
            {
                foreach (var s in config.states)
                {
                    if (s == null || s.parts == null)
                        continue;
                    foreach (var p in s.parts)
                    {
                        if (p.clip != clip)
                            continue;
                        Undo.RecordObject(s, "Delete Clip");
                        p.clip = null;
                        p.AnimatorStateAlias = null;
                        EditorUtility.SetDirty(s);
                    }
                }
            }

            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(clip));
            AssetDatabase.SaveAssets();
        }

        public static void SyncAliases(MultiAnimatorSession session)
        {
            var config = session.Config;
            if (config == null || config.states == null)
                return;

            foreach (var state in config.states)
            {
                if (state == null || state.parts == null)
                    continue;
                foreach (var part in state.parts)
                    if (SyncAlias(session, part))
                        EditorUtility.SetDirty(state);
            }
        }

        private static bool SyncAlias(MultiAnimatorSession session, PartEntry part)
        {
            if (part.clip == null || !session.TryGetAnimator(part, out var animator))
                return false;

            var stateName = FindAnimatorStateName(animator, part.clip);
            if (string.IsNullOrEmpty(stateName) || part.AnimatorStateAlias == stateName)
                return false;

            part.AnimatorStateAlias = stateName == part.clip.name ? null : stateName;
            return true;
        }

        private static string FindAnimatorStateName(Animator animator, AnimationClip clip)
        {
            if (animator.runtimeAnimatorController is not AnimatorController controller)
                return null;

            foreach (var layer in controller.layers)
            {
                var name = FindStateName(layer.stateMachine, clip);
                if (!string.IsNullOrEmpty(name))
                    return name;
            }
            return null;
        }

        private static string FindStateName(AnimatorStateMachine machine, AnimationClip clip)
        {
            foreach (var child in machine.states)
                if (child.state.motion == clip)
                    return child.state.name;

            foreach (var child in machine.stateMachines)
            {
                var name = FindStateName(child.stateMachine, clip);
                if (!string.IsNullOrEmpty(name))
                    return name;
            }
            return null;
        }

        private static PartEntry CopyPart(PartEntry source)
        {
            var copy = new PartEntry { partName = source.partName, clip = source.clip };
            var alias = source.AnimatorStateAlias;
            if (source.clip == null || alias != source.clip.name)
                copy.AnimatorStateAlias = alias;
            return copy;
        }

        private static string UniqueName(AnimationComposerConfig config, string baseName)
        {
            var names = new HashSet<string>();
            if (config.states != null)
                foreach (var s in config.states)
                    if (s != null)
                        names.Add(s.stateName);

            if (!names.Contains(baseName))
                return baseName;

            for (int i = 1; ; i++)
            {
                var candidate = $"{baseName} {i}";
                if (!names.Contains(candidate))
                    return candidate;
            }
        }
    }
}
