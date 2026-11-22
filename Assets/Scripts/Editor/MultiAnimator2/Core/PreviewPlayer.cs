using System;
using UnityEditor;
using UnityEngine;

namespace MultiAnimator2
{
    public sealed class PreviewPlayer : IDisposable
    {
        private readonly MultiAnimatorSession _session;
        private double _lastTick;

        public bool IsPlaying { get; private set; }
        public bool IsPreviewing => AnimationMode.InAnimationMode();

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
            {
                AnimationMode.StartAnimationMode();
                Sample();
            }
            else
            {
                IsPlaying = false;
                AnimationMode.StopAnimationMode();
            }
            StateChanged?.Invoke();
        }

        public void Play()
        {
            if (_session.State == null)
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
            if (!IsPreviewing || _session.State == null)
                return;

            AnimationMode.BeginSampling();
            foreach (var part in _session.Parts)
            {
                if (part.clip == null || !_session.TryGetAnimator(part, out var animator))
                    continue;
                AnimationMode.SampleAnimationClip(animator.gameObject, part.clip, _session.Time);
            }
            AnimationMode.EndSampling();
            SceneView.RepaintAll();
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

            if (!IsPreviewing || _session.State == null)
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
