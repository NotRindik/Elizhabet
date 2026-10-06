using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Controllers;
using Sirenix.OdinInspector;
using Systems;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ControllerDataTransfer : SerializedMonoBehaviour
{
    [SerializeField] private PlayerController source;
    [SerializeField] private OptimizedController target;
    [SerializeField] private string[] entries = Array.Empty<string>();
    [SerializeField] private bool remapSceneReferences = true;

    private Transform _srcRoot;
    private Transform _dstRoot;
    private readonly List<int> _path = new();

    [Button(ButtonSizes.Large)]
    public void Transfer()
    {
        if(source == null || target == null) return;

        _srcRoot = source.transform;
        _dstRoot = target.transform;

#if UNITY_EDITOR
        Undo.RecordObject(target, "Transfer controller data");
#endif

        var comps = new List<IComponent>(target.components);
        var syss = new List<ISystem>(target.systems);
        var count = 0;

        foreach(var name in entries)
        {
            var field = FindField(source.GetType(), name);
            if(field == null)
            {
                Debug.LogWarning($"[ControllerDataTransfer] Поле '{name}' не найдено в {source.GetType().Name}");
                continue;
            }

            var value = field.GetValue(source);
            if(value == null) continue;

            var copy = Clone(value);

            if(copy is IComponent c)
            {
                for(var i = comps.Count - 1; i >= 0; i--)
                    if(comps[i] != null && comps[i].GetType() == c.GetType()) comps.RemoveAt(i);
                comps.Add(c);
                count++;
            }
            else if(copy is ISystem s)
            {
                for(var i = syss.Count - 1; i >= 0; i--)
                    if(syss[i] != null && syss[i].GetType() == s.GetType()) syss.RemoveAt(i);
                syss.Add(s);
                count++;
            }
        }

        target.components = comps.ToArray();
        target.systems = syss.ToArray();

#if UNITY_EDITOR
        EditorUtility.SetDirty(target);
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
#endif
        Debug.Log($"[ControllerDataTransfer] Перенесено: {count}", target);
    }

    static FieldInfo FindField(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for(var t = type; t != null; t = t.BaseType)
        {
            foreach(var f in t.GetFields(flags))
                if(f.Name == name || f.FieldType.Name == name) return f;
        }
        return null;
    }

    object Clone(object src)
    {
        if(src == null) return null;

        var type = src.GetType();
        if(type.IsPrimitive || type.IsEnum || type == typeof(string)) return src;
        if(type.IsValueType && type.Namespace != null && type.Namespace.StartsWith("UnityEngine")) return src;
        if(typeof(UnityEngine.Object).IsAssignableFrom(type)) return Remap((UnityEngine.Object)src);
        if(src is Delegate) return null;

        if(type.IsArray)
        {
            var arr = (Array)src;
            var res = Array.CreateInstance(type.GetElementType(), arr.Length);
            for(var i = 0; i < arr.Length; i++)
                res.SetValue(Clone(arr.GetValue(i)), i);
            return res;
        }

        if(src is IDictionary dict)
        {
            var res = (IDictionary)CreateInstance(type);
            foreach(DictionaryEntry de in dict)
                res[Clone(de.Key)] = Clone(de.Value);
            return res;
        }

        if(src is IList list)
        {
            var res = (IList)CreateInstance(type);
            foreach(var item in list)
                res.Add(Clone(item));
            return res;
        }

        var copy = CreateInstance(type);
        for(var t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            foreach(var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if(!IsSerialized(f)) continue;
                f.SetValue(copy, Clone(f.GetValue(src)));
            }
        }
        return copy;
    }

    static object CreateInstance(Type type)
    {
        if(type.IsValueType || type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null) != null)
            return Activator.CreateInstance(type, true);
        return RuntimeHelpers.GetUninitializedObject(type);
    }

    static bool IsSerialized(FieldInfo f)
    {
        if(f.IsStatic || f.IsInitOnly || f.IsNotSerialized) return false;
        if(f.GetCustomAttribute<SerializeReference>() != null) return true;
        if(!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null) return false;
        return CanSerialize(f.FieldType);
    }

    static bool CanSerialize(Type t)
    {
        if(typeof(Delegate).IsAssignableFrom(t)) return false;
        if(t.IsPrimitive || t.IsEnum || t == typeof(string)) return true;
        if(typeof(UnityEngine.Object).IsAssignableFrom(t)) return true;
        if(t.IsValueType && t.Namespace != null && t.Namespace.StartsWith("UnityEngine")) return true;
        if(t.IsArray) return CanSerialize(t.GetElementType());
        if(t.IsInterface || t.IsAbstract) return false;
        return t.IsSerializable;
    }

    object Remap(UnityEngine.Object o)
    {
        if(!remapSceneReferences || o == null) return o;

        var go = o as GameObject;
        var comp = o as Component;
        var tr = go != null ? go.transform : comp != null ? comp.transform : null;
        if(tr == null || !tr.IsChildOf(_srcRoot)) return o;

        var mapped = MapTransform(tr);
        if(mapped == null)
        {
            Debug.LogWarning($"[ControllerDataTransfer] В целевой иерархии нет аналога для '{tr.name}'", target);
            return null;
        }

        if(go != null) return mapped.gameObject;
        return mapped.GetComponent(comp.GetType());
    }

    Transform MapTransform(Transform tr)
    {
        _path.Clear();
        for(var t = tr; t != _srcRoot; t = t.parent)
            _path.Add(t.GetSiblingIndex());

        var cur = _dstRoot;
        for(var i = _path.Count - 1; i >= 0; i--)
        {
            if(_path[i] >= cur.childCount) return null;
            cur = cur.GetChild(_path[i]);
        }
        return cur;
    }
}