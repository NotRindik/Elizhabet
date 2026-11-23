using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace MultiAnimator2
{
    public sealed class KeyRecorder : IDisposable
    {
        private readonly MultiAnimatorSession _session;
        private readonly PreviewPlayer _preview;

        public bool IsRecording { get; private set; }

        public event Action StateChanged;

        public KeyRecorder(MultiAnimatorSession session, PreviewPlayer preview)
        {
            _session = session;
            _preview = preview;
            _preview.StateChanged += OnPreviewChanged;
        }

        public void Dispose()
        {
            _preview.StateChanged -= OnPreviewChanged;
            SetRecording(false);
        }

        public void Toggle() => SetRecording(!IsRecording);

        public void SetRecording(bool on)
        {
            if (on == IsRecording)
                return;

            if (on)
            {
                if (!_session.HasContent)
                    return;
                _preview.SetPreview(true);
                Undo.postprocessModifications += OnPostprocess;
            }
            else
            {
                Undo.postprocessModifications -= OnPostprocess;
            }

            IsRecording = on;
            StateChanged?.Invoke();
        }

        private void OnPreviewChanged()
        {
            if (IsRecording && !_preview.IsPreviewing)
                SetRecording(false);
        }

        private UndoPropertyModification[] OnPostprocess(UndoPropertyModification[] mods)
        {
            if (!IsRecording || !_session.HasContent || !AnimationMode.InAnimationMode())
                return mods;

            bool changed = false;
            float time = _session.Time;

            foreach (var mod in mods)
            {
                if (mod.currentValue.target is not Component comp)
                    continue;

                if (comp is Transform && IsDrivenByColorPositioning(comp.transform))
                    continue;

                if (!TryFindOwner(comp.transform, out var part, out var path))
                    continue;

                var binding = new EditorCurveBinding
                {
                    path = path,
                    type = comp.GetType(),
                    propertyName = mod.currentValue.propertyPath
                };

                AnimationMode.AddPropertyModification(binding, mod.previousValue, false);

                bool isObject = mod.currentValue.objectReference != null || mod.previousValue.objectReference != null;
                if (isObject)
                {
                    Undo.RecordObject(part.clip, "Record Key");
                    var existing = AnimationUtility.GetObjectReferenceCurve(part.clip, binding)
                                   ?? Array.Empty<ObjectReferenceKeyframe>();
                    var list = new List<ObjectReferenceKeyframe>(existing);
                    list.RemoveAll(k => TimeKeys.Of(k.time) == TimeKeys.Of(time));
                    list.Add(new ObjectReferenceKeyframe { time = time, value = mod.currentValue.objectReference });
                    list.Sort((a, b) => a.time.CompareTo(b.time));
                    AnimationUtility.SetObjectReferenceCurve(part.clip, binding, list.ToArray());
                    EditorUtility.SetDirty(part.clip);
                    changed = true;
                    continue;
                }

                if (!float.TryParse(mod.currentValue.value, NumberStyles.Float, CultureInfo.InvariantCulture,
                        out float value))
                    continue;

                Undo.RecordObject(part.clip, "Record Key");
                var curve = AnimationUtility.GetEditorCurve(part.clip, binding) ?? new AnimationCurve();
                for (int k = curve.length - 1; k >= 0; k--)
                    if (TimeKeys.Of(curve[k].time) == TimeKeys.Of(time))
                        curve.RemoveKey(k);
                curve.AddKey(new Keyframe(time, value));
                AnimationUtility.SetEditorCurve(part.clip, binding, curve);
                EditorUtility.SetDirty(part.clip);
                changed = true;
            }

            if (changed)
                _session.Notify(SessionChange.Data);

            return mods;
        }

        private bool IsDrivenByColorPositioning(Transform target)
        {
            var preview = _session.ColorPreview;
            return _preview.UseColorPositioning && preview != null && preview.Drives(target);
        }

        private bool TryFindOwner(Transform target, out AnimationStateConfig.PartEntry owner, out string path)
        {
            owner = null;
            path = null;
            int bestDepth = int.MaxValue;

            foreach (var part in _session.Parts)
            {
                if (part.clip == null || !_session.TryGetAnimator(part, out var animator))
                    continue;
                if (target != animator.transform && !target.IsChildOf(animator.transform))
                    continue;

                var rel = AnimationUtility.CalculateTransformPath(target, animator.transform);
                int depth = string.IsNullOrEmpty(rel) ? 0 : rel.Split('/').Length;
                if (depth >= bestDepth)
                    continue;

                bestDepth = depth;
                owner = part;
                path = rel;
            }

            return owner != null;
        }
    }
}
