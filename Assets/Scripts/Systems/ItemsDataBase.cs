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
    private void LoadItems()
    {
        var prefabs = Resources.LoadAll<GameObject>(itemsResourcesPath);

        var list = new System.Collections.Generic.List<Item>();

        foreach (var prefab in prefabs)
        {
            if (prefab.TryGetComponent<Item>(out var item))
                list.Add(item);
        }

        items = list.ToArray();

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();

        Debug.Log($"Loaded {items.Length} items from Resources/{itemsResourcesPath}");
    }
#endif
}