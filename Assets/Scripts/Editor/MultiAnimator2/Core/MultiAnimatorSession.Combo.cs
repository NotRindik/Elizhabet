using System.Collections.Generic;
using UnityEngine;
using PartEntry = AnimationStateConfig.PartEntry;

namespace MultiAnimator2
{
    public enum ResolveKind
    {
        Playing,
        Held,
        Silent,
        Inactive
    }

    public sealed class ComboPart
    {
        public string PartName;
        public ResolveKind Kind = ResolveKind.Inactive;
        public AnimationLayerConfig OwnerLayer;
        public AnimationStateConfig OwnerState;
        public AnimationLayerConfig SourceLayer;
        public AnimationStateConfig SourceState;
    }

    public sealed partial class MultiAnimatorSession
    {
        public readonly Dictionary<AnimationLayerConfig, AnimationStateConfig> Combo = new();

        private readonly List<PartEntry> _comboParts = new();
        private readonly List<ComboPart> _comboInfo = new();

        public bool ComboMode { get; private set; }

        public IReadOnlyList<ComboPart> ComboInfo => _comboInfo;

        public bool HasLayers
        {
            get
            {
                var config = Config;
                return config != null && config.layers != null && config.layers.Count > 0;
            }
        }

        public ComboPart ComboInfoAt(int index) =>
            ComboMode && index >= 0 && index < _comboInfo.Count ? _comboInfo[index] : null;

        public AnimationStateConfig ComboStateOf(AnimationLayerConfig layer) =>
            layer != null && Combo.TryGetValue(layer, out var state) ? state : null;

        public void SetComboMode(bool on)
        {
            if (on == ComboMode || (on && !HasLayers))
                return;

            ComboMode = on;
            if (on && Combo.Count == 0)
                InitCombo();

            ResetView();
            ResolveCombo();
            Notify(SessionChange.State | SessionChange.Layout | SessionChange.Selection | SessionChange.Time | SessionChange.Data);
        }

        public void SetLayerState(AnimationLayerConfig layer, AnimationStateConfig state)
        {
            if (layer == null)
                return;

            if (state == null)
                Combo.Remove(layer);
            else
                Combo[layer] = state;

            ResetView();
            ResolveCombo();
            Notify(SessionChange.State | SessionChange.Layout | SessionChange.Selection | SessionChange.Time | SessionChange.Data);
        }

        public void ResolveCombo()
        {
            ClearCombo();
            var config = Config;
            if (!ComboMode || config == null || config.layers == null)
                return;

            var layers = config.layers;
            var order = new List<string>();
            foreach (var layer in layers)
                if (layer?.maskParts != null)
                    foreach (var part in layer.maskParts)
                        if (!string.IsNullOrEmpty(part) && !order.Contains(part))
                            order.Add(part);

            foreach (var partName in order)
            {
                var info = new ComboPart { PartName = partName };
                PartEntry entry = null;

                for (int i = layers.Count - 1; i >= 0; i--)
                {
                    var layer = layers[i];
                    if (!InMask(layer, partName))
                        continue;

                    var state = ComboStateOf(layer);
                    if (state == null)
                        continue;

                    info.OwnerLayer = layer;
                    info.OwnerState = state;
                    entry = FindPart(state, partName);

                    if (entry != null)
                    {
                        info.Kind = ResolveKind.Playing;
                        info.SourceLayer = layer;
                        info.SourceState = state;
                        break;
                    }

                    info.Kind = ResolveKind.Silent;
                    for (int j = i - 1; j >= 0; j--)
                    {
                        var lower = layers[j];
                        var lowerState = InMask(lower, partName) ? ComboStateOf(lower) : null;
                        var lowerEntry = FindPart(lowerState, partName);
                        if (lowerEntry == null)
                            continue;

                        entry = lowerEntry;
                        info.Kind = ResolveKind.Held;
                        info.SourceLayer = lower;
                        info.SourceState = lowerState;
                        break;
                    }
                    break;
                }

                _comboInfo.Add(info);
                _comboParts.Add(entry ?? new PartEntry { partName = partName });
            }

            Time = Mathf.Min(Time, Length);
        }

        private void InitCombo()
        {
            var config = Config;
            foreach (var layer in config.layers)
            {
                if (layer?.states == null)
                    continue;
                if (State != null && layer.states.Contains(State))
                    Combo[layer] = State;
            }

            if (Combo.Count > 0)
                return;

            foreach (var layer in config.layers)
            {
                if (layer?.states == null)
                    continue;
                foreach (var s in layer.states)
                {
                    if (s == null)
                        continue;
                    Combo[layer] = s;
                    return;
                }
            }
        }

        private void ValidateCombo()
        {
            var dead = new List<AnimationLayerConfig>();
            var config = Config;
            foreach (var (layer, state) in Combo)
            {
                bool alive = state != null && config != null && config.layers != null && config.layers.Contains(layer) &&
                             layer.states != null && layer.states.Contains(state);
                if (!alive)
                    dead.Add(layer);
            }
            foreach (var layer in dead)
                Combo.Remove(layer);
            ResolveCombo();
        }

        private void ClearCombo()
        {
            _comboParts.Clear();
            _comboInfo.Clear();
        }

        private void ResetView()
        {
            Keys.Clear();
            Expanded.Clear();
            FocusedPart = -1;
        }

        private static bool InMask(AnimationLayerConfig layer, string partName) =>
            layer?.maskParts != null && layer.maskParts.Contains(partName);

        private static PartEntry FindPart(AnimationStateConfig state, string partName)
        {
            if (state == null || state.parts == null)
                return null;
            foreach (var p in state.parts)
                if (p.partName == partName && p.clip != null)
                    return p;
            return null;
        }
    }
}
