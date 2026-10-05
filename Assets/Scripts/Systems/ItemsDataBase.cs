using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(menuName = "Game/Items Database")]
public class ItemsDataBase : SerializedScriptableObject
{
    [FolderPath(ParentFolder = "Assets/Resources")]
    public string itemsResourcesPath = "Prefabs/Items";

    [ReadOnly]
    public Item[] items;
    
    private Dictionary<string, Item> _lookup;
    
    public IReadOnlyList<Item> Items => items;

    private void OnEnable()
    {
        BuildLookup();
    }

    private void BuildLookup()
    {
        _lookup = new Dictionary<string, Item>(items.Length);

        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            if (item == null) continue;

            _lookup[item.name] = item;
        }
    }

    public Item Get(string name)
    {
        _lookup.TryGetValue(name, out var item);
        return item;
    }

#if UNITY_EDITOR
    [Button("Load Items From Resources")]
    public void LoadItems()
    {
        var prefabs = Resources.LoadAll<GameObject>(itemsResourcesPath);

        var list = new List<Item>();
        foreach (var prefab in prefabs)
            if (prefab.TryGetComponent<Item>(out var item))
                list.Add(item);

        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name)); // стабильный порядок
        
        if (items != null && items.Length == list.Count)
        {
            bool same = true;
            for (int i = 0; i < items.Length; i++)
                if (items[i] != list[i]) { same = false; break; }
            if (same) return;
        }

        items = list.ToArray();

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        BuildLookup();
        Debug.Log($"Loaded {items.Length} items from Resources/{itemsResourcesPath}");
    }
#endif
}

#if UNITY_EDITOR
public class ItemsDataBasePostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (!HasRelevant(imported) && !HasRelevant(deleted) &&
            !HasRelevant(moved) && !HasRelevant(movedFrom))
            return;
        
        EditorApplication.delayCall += RefreshAll;
    }
    
    private static bool HasRelevant(string[] paths)
    {
        foreach (var p in paths)
            if (p.EndsWith(".prefab") && p.Contains("/Resources/"))
                return true;
        return false;
    }[UnityEditor.Callbacks.DidReloadScripts]

    private static void RefreshAll()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:ItemsDataBase"))
        {
            var db = AssetDatabase.LoadAssetAtPath<ItemsDataBase>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (db != null) db.LoadItems();
        }
    }
}
#endif