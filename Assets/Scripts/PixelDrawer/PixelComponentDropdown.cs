#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

public class PixelComponentDropdown : AdvancedDropdown
{
    static readonly string[] Recommended =
    {
        "PixelLayerCollider",
        "UnityEngine.Rendering.SortingGroup",
        "UnityEngine.Rendering.Universal.Light2D",
        "UnityEngine.Rendering.Universal.ShadowCaster2D",
        "UnityEngine.PolygonCollider2D",
        "UnityEngine.BoxCollider2D",
        "UnityEngine.CompositeCollider2D",
        "UnityEngine.Rigidbody2D"
    };

    static List<Type> cachedTypes;

    readonly GameObject target;
    readonly Dictionary<AdvancedDropdownItem, Type> types = new Dictionary<AdvancedDropdownItem, Type>();

    public PixelComponentDropdown(AdvancedDropdownState state, GameObject target) : base(state)
    {
        this.target = target;
        minimumSize = new Vector2(260, 340);
    }

    protected override AdvancedDropdownItem BuildRoot()
    {
        types.Clear();
        var root = new AdvancedDropdownItem("Add Component");
        var all = AllTypes();

        var recommended = new AdvancedDropdownItem("Recommended");
        var recommendedCount = 0;
        foreach(var name in Recommended)
        {
            var type = all.Find(t => t.FullName == name);
            if(type == null) continue;
            recommended.AddChild(Item(type));
            recommendedCount++;
        }
        if(recommendedCount > 0) root.AddChild(recommended);

        var groups = new SortedDictionary<string, AdvancedDropdownItem>(StringComparer.OrdinalIgnoreCase);
        foreach(var type in all)
        {
            var group = string.IsNullOrEmpty(type.Namespace) ? "Scripts" : type.Namespace;
            AdvancedDropdownItem item;
            if(!groups.TryGetValue(group, out item))
            {
                item = new AdvancedDropdownItem(group);
                groups[group] = item;
            }
            item.AddChild(Item(type));
        }
        foreach(var group in groups.Values) root.AddChild(group);
        return root;
    }

    AdvancedDropdownItem Item(Type type)
    {
        var item = new AdvancedDropdownItem(ObjectNames.NicifyVariableName(type.Name));
        var icon = AssetPreview.GetMiniTypeThumbnail(type);
        if(icon != null) item.icon = icon as Texture2D;
        types[item] = type;
        return item;
    }

    protected override void ItemSelected(AdvancedDropdownItem item)
    {
        Type type;
        if(target == null || !types.TryGetValue(item, out type)) return;
        if(Attribute.IsDefined(type, typeof(DisallowMultipleComponent), true) && target.GetComponent(type) != null)
        {
            Debug.LogWarning(type.Name + " is already on " + target.name, target);
            return;
        }
        Undo.AddComponent(target, type);
    }

    static List<Type> AllTypes()
    {
        if(cachedTypes != null) return cachedTypes;
        cachedTypes = new List<Type>();
        foreach(var type in TypeCache.GetTypesDerivedFrom<Component>())
        {
            if(type.IsAbstract || type.IsGenericTypeDefinition || !type.IsVisible) continue;
            if(typeof(Transform).IsAssignableFrom(type)) continue;
            var assembly = type.Assembly.GetName().Name;
            if(assembly.StartsWith("UnityEditor") || assembly.Contains(".Editor") || assembly.EndsWith("-Editor") || assembly.EndsWith(".Tests")) continue;
            var menu = (AddComponentMenu)Attribute.GetCustomAttribute(type, typeof(AddComponentMenu));
            if(menu != null && string.IsNullOrEmpty(menu.componentMenu)) continue;
            cachedTypes.Add(type);
        }
        cachedTypes.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return cachedTypes;
    }
}
#endif
