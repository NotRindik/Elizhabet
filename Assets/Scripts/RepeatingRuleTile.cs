using UnityEngine;
using AYellowpaper.SerializedCollections;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Tiles/Patterned Rule Tile")]
public class PatternedRuleTile : RuleTile
{
    [Header("Pattern")]
    public int seed;
    public bool useChecker;

    public PatternLibrary patternLibrary = new();

    public override void GetTileData(
        Vector3Int position,
        ITilemap tilemap,
        ref TileData tileData)
    {
        // 1️⃣ СНАЧАЛА стандартная RuleTile логика
        base.GetTileData(position, tilemap, ref tileData);

        if (tileData.sprite == null)
            return;

        // 2️⃣ Получаем варианты для этого спрайта
        if (!patternLibrary.TryGetVariants(tileData.sprite, out var variants))
            return;

        // 3️⃣ Выбираем вариант по позиции
        int index = GetPatternIndex(position, variants.Length);
        tileData.sprite = variants[index];
    }

    int GetPatternIndex(Vector3Int pos, int count)
    {
        if (useChecker)
            return Mathf.Abs((pos.x + pos.y) % count);

        int hash =
            pos.x * 73856093 ^
            pos.y * 19349663 ^
            seed;

        return Mathf.Abs(hash) % count;
    }
}

[System.Serializable]
public class PatternLibrary
{
    public SerializedDictionary<Sprite, Sprite[]> map = new();

    public void Register(Sprite baseSprite, Sprite[] variants)
    {
        map[baseSprite] = variants;
    }

    public bool TryGetVariants(Sprite baseSprite, out Sprite[] variants)
        => map.TryGetValue(baseSprite, out variants);
}
