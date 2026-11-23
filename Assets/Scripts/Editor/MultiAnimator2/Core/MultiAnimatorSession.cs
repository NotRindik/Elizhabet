using System;
using System.Collections.Generic;
using UnityEngine;

namespace MultiAnimator2
{
    [Flags]
    public enum SessionChange
    {
        None = 0,
        Root = 1,
        State = 2,
        Time = 4,
        Selection = 8,
        Data = 16,
        Layout = 32,
        All = ~0
    }

    public sealed partial class MultiAnimatorSession
    {
        private static readonly List<AnimationStateConfig.PartEntry> NoParts = new();

        public readonly Dictionary<string, Animator> Animators = new();
        public readonly KeySelection Keys = new();
        public readonly HashSet<int> Expanded = new();

        public AnimationComposerTag Tag { get; private set; }
        public AnimationStateConfig State { get; private set; }
        public float Time { get; private set; }
        public int FocusedPart { get; private set; } = -1;
        public Systems.ColorPositioningPreview ColorPreview { get; private set; }

        public event Action<SessionChange> Changed;

        public AnimationComposerConfig Config => Tag != null ? Tag.config : null;
        public GameObject Root => Tag != null ? Tag.gameObject : null;

        public List<AnimationStateConfig.PartEntry> Parts =>
            ComboMode ? _comboParts : State != null && State.parts != null ? State.parts : NoParts;

        public bool HasContent => ComboMode ? _comboParts.Count > 0 : State != null;

        public bool CanEditStructure => !ComboMode && State != null;

        public AnimationStateConfig.PartEntry FocusedEntry =>
            FocusedPart >= 0 && FocusedPart < Parts.Count ? Parts[FocusedPart] : null;

        public float Length
        {
            get
            {
                float max = 0f;
                foreach (var p in Parts)
                    if (p.clip != null)
                        max = Mathf.Max(max, p.clip.length);
                return max > 0f ? max : 1f;
            }
        }

        public float FrameRate
        {
            get
            {
                float max = 0f, any = 0f;
                foreach (var p in Parts)
                {
                    if (p.clip == null)
                        continue;
                    any = Mathf.Max(any, p.clip.frameRate);
                    if (IsTiming(p.clip))
                        max = Mathf.Max(max, p.clip.frameRate);
                }
                if (max > 0f)
                    return max;
                return any > 0f ? any : 30f;
            }
        }

        public bool HasMixedFrameRates
        {
            get
            {
                float first = -1f;
                foreach (var p in Parts)
                {
                    if (p.clip == null || !IsTiming(p.clip))
                        continue;
                    if (first < 0f)
                        first = p.clip.frameRate;
                    else if (!Mathf.Approximately(first, p.clip.frameRate))
                        return true;
                }
                return false;
            }
        }

        public static bool IsTiming(AnimationClip clip) => clip.length * clip.frameRate >= 0.5f;

        public int Frame => Mathf.RoundToInt(Time * FrameRate);

        public int FrameCount => Mathf.RoundToInt(Length * FrameRate);

        public void Notify(SessionChange change)
        {
            if (change != SessionChange.None)
                Changed?.Invoke(change);
        }

        public void SetTag(AnimationComposerTag tag)
        {
            Tag = tag;
            State = null;
            ComboMode = false;
            Combo.Clear();
            ClearCombo();
            Time = 0f;
            FocusedPart = -1;
            Keys.Clear();
            Expanded.Clear();
            RefreshAnimators();

            var config = Config;
            if (config != null && config.states != null)
            {
                foreach (var s in config.states)
                {
                    if (s == null)
                        continue;
                    State = s;
                    break;
                }
            }

            Notify(SessionChange.All);
        }

        public void RefreshAnimators()
        {
            Animators.Clear();
            AnimatorRegistry.Animators.Clear();
            ColorPreview = null;

            if (Root == null)
                return;

            ColorPreview = Root.GetComponentInChildren<Systems.ColorPositioningPreview>(true);

            foreach (var a in Root.GetComponentsInChildren<Animator>(true))
            {
                Animators[a.gameObject.name] = a;
                AnimatorRegistry.Animators[a.gameObject.name] = a;
            }

            StateAssets.SyncAliases(this);
        }

        public void SelectState(AnimationStateConfig state)
        {
            if (state == State)
                return;

            State = state;
            Keys.Clear();
            Expanded.Clear();
            FocusedPart = -1;
            Time = Mathf.Min(Time, Length);
            Notify(SessionChange.State | SessionChange.Selection | SessionChange.Time | SessionChange.Layout);
        }

        public void ValidateState()
        {
            if (ComboMode)
                ValidateCombo();

            var config = Config;
            if (State != null && config != null && config.states != null && config.states.Contains(State))
                return;

            State = null;
            if (config != null && config.states != null)
            {
                foreach (var s in config.states)
                {
                    if (s == null)
                        continue;
                    State = s;
                    break;
                }
            }
            Keys.Clear();
            FocusedPart = -1;
            Expanded.Clear();
        }

        public void FocusPart(int index)
        {
            if (index == FocusedPart)
                return;
            FocusedPart = index;
            Notify(SessionChange.Selection);
        }

        public void ToggleExpanded(int index)
        {
            if (!Expanded.Remove(index))
                Expanded.Add(index);
            Notify(SessionChange.Layout);
        }

        public bool TryGetAnimator(AnimationStateConfig.PartEntry part, out Animator animator)
        {
            animator = null;
            return part != null && !string.IsNullOrEmpty(part.partName) &&
                   Animators.TryGetValue(part.partName, out animator) && animator != null;
        }

        public int PartIndexOf(AnimationClip clip)
        {
            for (int i = 0; i < Parts.Count; i++)
                if (Parts[i].clip == clip)
                    return i;
            return -1;
        }

        public float Snap(float time)
        {
            float fps = FrameRate;
            return Mathf.Round(time * fps) / fps;
        }

        public void SetTime(float time, bool snap = true)
        {
            time = Mathf.Clamp(snap ? Snap(time) : time, 0f, Length);
            if (Mathf.Approximately(time, Time))
                return;
            Time = time;
            Notify(SessionChange.Time);
        }

        public void SetFrame(int frame) => SetTime(frame / FrameRate);

        public void PruneSelection() => Keys.RemoveWhere(k => !KeyOps.Exists(k));
    }
}
