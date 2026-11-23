using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace MultiAnimator2
{
    public sealed class PreviewPlayer : IDisposable
    {
        private static readonly string[] PositionProperties = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };

        private readonly MultiAnimatorSession _session;
        private readonly Dictionary<Transform, Vector3> _originals = new();
        private readonly HashSet<Transform> _registered = new();
        private double _lastTick;

        public bool IsPlaying { get; private set; }
        public bool IsPreviewing => AnimationMode.InAnimationMode();
        public bool UseColorPositioning { get; private set; } = true;
        public bool HasColorPositioning => _session.ColorPreview != null && _session.ColorPreview.IsAvailable;
        public bool ColorPositioningActive => UseColorPositioning && HasColorPositioning;

        public event Action StateChanged;

        public PreviewPlayer(MultiAnimatorSession session)
        {
            _session = session;
            _session.Changed += OnSessionChanged;
            EditorApplication.update += Tick;
        }

        public void Dispose()
        {
            _session.Changed -= OnSessionChanged;
            EditorApplication.update -= Tick;
            SetPreview(false);
        }

        public void SetPreview(bool on)
        {
            if (on == IsPreviewing)
                return;

            if (on)
                StartMode();
            else
            {
                IsPlaying = false;
                StopMode();
            }
            StateChanged?.Invoke();
        }

        public void SetColorPositioning(bool on)
        {
            if (on == UseColorPositioning)
                return;

            UseColorPositioning = on;
            if (IsPreviewing)
            {
                StopMode();
                StartMode();
            }
            StateChanged?.Invoke();
        }

        public void Play()
        {
            if (!_session.HasContent)
                return;
            SetPreview(true);
            IsPlaying = true;
            _lastTick = EditorApplication.timeSinceStartup;
            StateChanged?.Invoke();
        }

        public void Pause()
        {
            if (!IsPlaying)
                return;
            IsPlaying = false;
            _session.SetTime(_session.Time);
            StateChanged?.Invoke();
        }

        public void TogglePlay()
        {
            if (IsPlaying)
                Pause();
            else
                Play();
        }

        public void Sample()
        {
            if (!IsPreviewing || !_session.HasContent)
                return;

            AnimationMode.BeginSampling();
            foreach (var part in _session.Parts)
            {
                if (part.clip == null || !_session.TryGetAnimator(part, out var animator))
                    continue;
                AnimationMode.SampleAnimationClip(animator.gameObject, part.clip, _session.Time);
            }
            RegisterColorSetters();
            AnimationMode.EndSampling();

            if (UseColorPositioning && _session.ColorPreview != null)
                _session.ColorPreview.Apply();
            SceneView.RepaintAll();
        }

        private void StartMode()
        {
            _originals.Clear();
            var preview = _session.ColorPreview;
            if (preview != null)
            {
                preview.ClearCache();
                if (UseColorPositioning)
                    foreach (var setter in preview.Setters)
                        if (setter != null)
                            _originals[setter.transform] = setter.transform.localPosition;
            }
            AnimationMode.StartAnimationMode();
            Sample();
        }

        private void StopMode()
        {
            foreach (var (target, position) in _originals)
                if (target != null)
                    target.localPosition = position;
            _originals.Clear();
            _registered.Clear();
            AnimationMode.StopAnimationMode();
        }

        private void RegisterColorSetters()
        {
            var preview = _session.ColorPreview;
            if (!UseColorPositioning || preview == null || _session.Root == null)
                return;

            var root = _session.Root.transform;
            foreach (var setter in preview.Setters)
                if (setter != null)
                    Register(setter.transform, root);
        }

        private void Register(Transform target, Transform root)
        {
            if (!_registered.Add(target))
                return;
            if (!_originals.TryGetValue(target, out var local))
                _originals[target] = local = target.localPosition;

            var path = AnimationUtility.CalculateTransformPath(target, root);
            for (int i = 0; i < PositionProperties.Length; i++)
            {
                var binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), PositionProperties[i]);
                var modification = new PropertyModification
                {
                    target = target,
                    propertyPath = PositionProperties[i],
                    value = local[i].ToString(CultureInfo.InvariantCulture)
                };
                AnimationMode.AddPropertyModification(binding, modification, false);
            }
        }

        private void OnSessionChanged(SessionChange change)
        {
            if ((change & SessionChange.Root) != 0)
                SetPreview(false);

            if ((change & (SessionChange.Time | SessionChange.State | SessionChange.Data)) != 0)
                Sample();
        }

        private void Tick()
        {
            if (!IsPlaying)
                return;

            if (!IsPreviewing || !_session.HasContent)
            {
                IsPlaying = false;
                StateChanged?.Invoke();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _lastTick);
            _lastTick = now;
            _session.SetTime(Mathf.Repeat(_session.Time + dt, _session.Length), false);
        }
    }
}
