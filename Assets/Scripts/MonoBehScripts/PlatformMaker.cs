using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Sirenix.OdinInspector;

public class PlatformMaker : MonoBehaviour
{
    [Required]
    private Tilemap tilemap; // ������� � �����������

    [BoxGroup("Settings")]
    private GameObject platformParent => transform.gameObject; // ���� ��������� �����

    public bool GenerateOnValidate = false;

    [BoxGroup("Settings")]
    public string blockName = "PlatformBlock";

    [BoxGroup("Settings")]
    public Vector2 tileSizeOffset = Vector2.zero;
    public Vector2 tilePosOffset = Vector2.zero;

    [BoxGroup("Settings")]
    private Vector2 tileSize = Vector2.one;
    
    [BoxGroup("Settings")]
    public ObjectAudioMaterial tileAudioMaterial;


    private void OnValidate()
    {
        tilemap ??= GetComponent<Tilemap>();

        tileSize = tilemap.cellSize;

        if (GenerateOnValidate)
            GeneratePlatforms();
    }

    [Button("Generate Platforms")]
    private void GeneratePlatforms()
    {
        if (tilemap == null)
        {
            Debug.LogWarning("Tilemap not assigned!");
            return;
        }

        ClearPlatforms();

        BoundsInt bounds = tilemap.cellBounds;
        bool[,] processed = new bool[bounds.size.x, bounds.size.y];

        for (int x = 0; x < bounds.size.x; x++)
        {
            for (int y = 0; y < bounds.size.y; y++)
            {
                if (processed[x, y]) continue;

                Vector3Int cellPos = new Vector3Int(bounds.xMin + x, bounds.yMin + y, 0);
                if (tilemap.HasTile(cellPos))
                {
                    // ����� ������������� ����
                    Vector2Int blockSize = GetBlockSize(bounds, x, y, processed);
                    CreateBlock(cellPos, blockSize);
                }
            }
        }

        Debug.Log("Platform generation finished!");
    }

    [Button("Clear Platforms")]
    private void ClearPlatforms()
    {
        if (platformParent == null) return;
        for (int i = platformParent.transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(platformParent.transform.GetChild(i).gameObject);
        }
    }

    private Vector2Int GetBlockSize(BoundsInt bounds, int startX, int startY, bool[,] processed)
    {
        int width = 1;
        int height = 1;

        // ��������� ������
        for (int x = startX + 1; x < bounds.size.x; x++)
        {
            if (processed[x, startY]) break;
            Vector3Int cellPos = new Vector3Int(bounds.xMin + x, bounds.yMin + startY, 0);
            if (tilemap.HasTile(cellPos)) width++;
            else break;
        }

        // ��������� ������
        for (int y = startY + 1; y < bounds.size.y; y++)
        {
            bool fullRow = true;
            for (int x = startX; x < startX + width; x++)
            {
                if (processed[x, y])
                {
                    fullRow = false;
                    break;
                }
                Vector3Int cellPos = new Vector3Int(bounds.xMin + x, bounds.yMin + y, 0);
                if (!tilemap.HasTile(cellPos))
                {
                    fullRow = false;
                    break;
                }
            }

            if (fullRow) height++;
            else break;
        }

        // �������� ������������
        for (int x = startX; x < startX + width; x++)
        {
            for (int y = startY; y < startY + height; y++)
            {
                processed[x, y] = true;
            }
        }

        return new Vector2Int(width, height);
    }

    private void CreateBlock(Vector3Int cellStart, Vector2Int blockSize)
    {
        Vector3 worldPos = tilemap.CellToWorld(cellStart);

        GameObject block = new GameObject(blockName);
        block.transform.parent = platformParent.transform;

        BoxCollider2D col = block.AddComponent<BoxCollider2D>();
        col.size = new Vector2(blockSize.x * tileSize.x + tileSizeOffset.x, blockSize.y * tileSize.y + tileSizeOffset.y);
        col.offset = new Vector2(col.size.x / 2f + tilePosOffset.x, col.size.y / 2f + tilePosOffset.y);
        col.usedByEffector = true;
        block.layer = LayerMask.NameToLayer("Platform");

        PlatformEffector2D effector = block.AddComponent<PlatformEffector2D>();
        effector.surfaceArc = 179f; // ���������� ��� ��������

        AudioMaterialSetter materialSetter = block.AddComponent<AudioMaterialSetter>();
        materialSetter.AudioMaterial = tileAudioMaterial;
        // ������������� ����
        block.transform.position = worldPos;
    }
}