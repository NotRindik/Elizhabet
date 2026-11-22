using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MultiAnimator2
{
    public readonly struct CurveId : IEquatable<CurveId>
    {
        public readonly AnimationClip Clip;
        public readonly EditorCurveBinding Binding;
        public readonly bool IsObject;

        public CurveId(AnimationClip clip, EditorCurveBinding binding, bool isObject)
        {
            Clip = clip;
            Binding = binding;
            IsObject = isObject;
        }

        public bool Equals(CurveId other) =>
            ReferenceEquals(Clip, other.Clip) && IsObject == other.IsObject && Binding.Equals(other.Binding);

        public override bool Equals(object obj) => obj is CurveId other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Clip != null ? Clip.GetInstanceID() : 0, Binding.GetHashCode(), IsObject);
    }

    public readonly struct KeyRef : IEquatable<KeyRef>
    {
        public readonly AnimationClip Clip;
        public readonly EditorCurveBinding Binding;
        public readonly float Time;
        public readonly bool IsObject;

        public KeyRef(AnimationClip clip, EditorCurveBinding binding, float time, bool isObject)
        {
            Clip = clip;
            Binding = binding;
            Time = time;
            IsObject = isObject;
        }

        public CurveId Curve => new(Clip, Binding, IsObject);

        public int TimeKey => TimeKeys.Of(Time);

        public bool Equals(KeyRef other) =>
            ReferenceEquals(Clip, other.Clip) && IsObject == other.IsObject &&
            TimeKey == other.TimeKey && Binding.Equals(other.Binding);

        public override bool Equals(object obj) => obj is KeyRef other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Clip != null ? Clip.GetInstanceID() : 0, Binding.GetHashCode(), TimeKey, IsObject);
    }

    public static class TimeKeys
    {
        public static int Of(float time) => Mathf.RoundToInt(time * 10000f);
    }

    public sealed class CurveSnapshot
    {
        public Keyframe[] Keys = Array.Empty<Keyframe>();
        public ObjectReferenceKeyframe[] ObjectKeys = Array.Empty<ObjectReferenceKeyframe>();
        public WrapMode PreWrap;
        public WrapMode PostWrap;
    }

    public struct ClipboardEntry
    {
        public EditorCurveBinding Binding;
        public bool IsObject;
        public Keyframe Key;
        public Object Value;
        public float Time;
    }

    public sealed class KeySelection : IEnumerable<KeyRef>
    {
        private readonly HashSet<KeyRef> _keys = new();

        public int Count => _keys.Count;

        public bool Contains(KeyRef key) => _keys.Contains(key);

        public void Add(KeyRef key) => _keys.Add(key);

        public void AddRange(IEnumerable<KeyRef> keys)
        {
            foreach (var k in keys)
                _keys.Add(k);
        }

        public void Remove(KeyRef key) => _keys.Remove(key);

        public void Clear() => _keys.Clear();

        public void Set(IEnumerable<KeyRef> keys)
        {
            _keys.Clear();
            AddRange(keys);
        }

        public void RemoveWhere(Predicate<KeyRef> match) => _keys.RemoveWhere(match);

        public List<KeyRef> ToList() => new(_keys);

        public IEnumerator<KeyRef> GetEnumerator() => _keys.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
