using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Tilemaps;
[System.Serializable]
public class TileGroup
{
    public ObjectAudioMaterial material;
    public TileBase[] tiles;
}
[CreateAssetMenu(menuName = "Audio/Tile Audio Database")]
public class TileAudioDatabase : SerializedScriptableObject
{
    public TileGroup[] groups;

    public Dictionary<TileBase, ObjectAudioMaterial> cache;

    
    [Button("Refresh")]
    public void Init()
    {
        cache = new Dictionary<TileBase, ObjectAudioMaterial>();

        foreach (var g in groups)
        {
            foreach (var tile in g.tiles)
            {
                cache[tile] = g.material;
            }
        }
    }

    public ObjectAudioMaterial Get(TileBase tile)
    {
        if (tile == null)
            return null;

        if (cache == null)
            Init();

        cache.TryGetValue(tile, out var mat);
        return mat;
    }
}
