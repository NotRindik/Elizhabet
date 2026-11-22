using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MultiAnimator2
{
    public sealed class PropertyGroup
    {
        public string Label;
        public string Path;
        public int Depth;
        public bool IsObject;
        public EditorCurveBinding[] Bindings;
        public float[] Times;
    }

    public sealed class ClipInfo
    {
        public readonly List<PropertyGroup> Groups = new();
        public readonly HashSet<int> ObjectTimes = new();
        public readonly Dictionary<int, int> GroupsPerTime = new();
        public float[] Times = Array.Empty<float>();
    }

    [InitializeOnLoad]
    public static class ClipCache
    {
        private static readonly Dictionary<AnimationClip, ClipInfo> Cache = new();

        public static event Action Invalidated;

        static ClipCache()
        {
            AnimationUtility.onCurveWasModified += OnCurveModified;
            Undo.undoRedoPerformed += Clear;
        }

        public static ClipInfo Get(AnimationClip clip)
        {
            if (clip == null)
                return null;

            if (!Cache.TryGetValue(clip, out var info))
            {
                info = Build(clip);
                Cache[clip] = info;
            }
            return info;
        }

        public static void Invalidate(AnimationClip clip)
        {
            if (clip != null && Cache.Remove(clip))
                Invalidated?.Invoke();
        }

        public static void Clear()
        {
            Cache.Clear();
            Invalidated?.Invoke();
        }

        private static void OnCurveModified(AnimationClip clip, EditorCurveBinding binding,
            AnimationUtility.CurveModifiedType type) => Invalidate(clip);

        private static ClipInfo Build(AnimationClip clip)
        {
            var info = new ClipInfo();
            var indexByKey = new Dictionary<string, int>();
            var floatBindings = new List<List<EditorCurveBinding>>();

            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                var baseName = StripAxis(b.propertyName);
                var key = b.path + "|" + (b.type != null ? b.type.FullName : "") + "|" + baseName;

                if (!indexByKey.TryGetValue(key, out var index))
                {
                    index = info.Groups.Count;
                    indexByKey[key] = index;
                    var (label, depth) = BuildLabel(b.path, b.type, baseName);
                    info.Groups.Add(new PropertyGroup { Label = label, Path = b.path, Depth = depth });
                    floatBindings.Add(new List<EditorCurveBinding>());
                }
                floatBindings[index].Add(b);
            }

            for (int i = 0; i < floatBindings.Count; i++)
            {
                info.Groups[i].Bindings = floatBindings[i].ToArray();
                info.Groups[i].Times = FloatTimes(clip, info.Groups[i].Bindings);
            }

            foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var (label, depth) = BuildLabel(b.path, b.type, b.propertyName);
                info.Groups.Add(new PropertyGroup
                {
                    Label = label,
                    Path = b.path,
                    Depth = depth,
                    IsObject = true,
                    Bindings = new[] { b },
                    Times = ObjectTimes(clip, b)
                });
            }

            info.Groups.Sort((x, y) =>
            {
                int c = string.CompareOrdinal(x.Path, y.Path);
                return c != 0 ? c : string.CompareOrdinal(x.Label, y.Label);
            });

            var all = new SortedDictionary<int, float>();
            foreach (var g in info.Groups)
            {
                foreach (var t in g.Times)
                {
                    int k = TimeKeys.Of(t);
                    all.TryAdd(k, t);
                    info.GroupsPerTime.TryGetValue(k, out var n);
                    info.GroupsPerTime[k] = n + 1;
                    if (g.IsObject)
                        info.ObjectTimes.Add(k);
                }
            }

            info.Times = new float[all.Count];
            all.Values.CopyTo(info.Times, 0);
            return info;
        }

        private static float[] FloatTimes(AnimationClip clip, EditorCurveBinding[] bindings)
        {
            var times = new SortedDictionary<int, float>();
            foreach (var b in bindings)
            {
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                if (curve == null)
                    continue;
                foreach (var k in curve.keys)
                    times.TryAdd(TimeKeys.Of(k.time), k.time);
            }
            var result = new float[times.Count];
            times.Values.CopyTo(result, 0);
            return result;
        }

        private static float[] ObjectTimes(AnimationClip clip, EditorCurveBinding binding)
        {
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (keys == null)
                return Array.Empty<float>();

            var result = new float[keys.Length];
            for (int i = 0; i < keys.Length; i++)
                result[i] = keys[i].time;
            return result;
        }

        public static string StripAxis(string propertyName)
        {
            int n = propertyName.Length;
            if (n > 2 && propertyName[n - 2] == '.')
            {
                char last = propertyName[n - 1];
                if (last is 'x' or 'y' or 'z' or 'w')
                    return propertyName.Substring(0, n - 2);
            }
            return propertyName;
        }

        public static string Nicify(string name)
        {
            var p = name.StartsWith("m_") ? name.Substring(2) : name;
            return p.Length > 0 ? char.ToUpperInvariant(p[0]) + p.Substring(1) : p;
        }

        private static (string label, int depth) BuildLabel(string path, Type type, string baseName)
        {
            string segment = "";
            int depth = 0;
            if (!string.IsNullOrEmpty(path))
            {
                var parts = path.Split('/');
                segment = parts[^1];
                depth = parts.Length;
            }

            var prop = Nicify(baseName);
            var text = type == typeof(Transform) ? prop : $"{(type != null ? type.Name : "?")}.{prop}";
            return (string.IsNullOrEmpty(segment) ? text : $"{segment}: {text}", depth);
        }
    }
}
