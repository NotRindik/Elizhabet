using System.Collections.Generic;
using UnityEngine;

public class MeshGenerator : MonoBehaviour
{
    [SerializeField] private Material skinMaterial; // Можно задать в инспекторе

    void Start()
    {
        var points = new List<Vector2>()
        {
            new Vector2(0, 0),
            new Vector2(1, 0.2f),
            new Vector2(2, 0.5f),
            new Vector2(3, 0.7f)
        };

        float width = 0.5f;

        // Создаем GameObject
        GameObject hand = new GameObject("ProceduralHand");
        hand.transform.position = Vector3.zero;

        // Добавляем компоненты
        var meshFilter = hand.AddComponent<MeshFilter>();
        var meshRenderer = hand.AddComponent<MeshRenderer>();

        // Генерируем меш
        meshFilter.mesh = GenerateHandMesh(points, width);

        // Генерируем или присваиваем материал
        if (skinMaterial != null)
            meshRenderer.material = skinMaterial;
        else
        {
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = Color.red;
            meshRenderer.material = mat;
        }
    }

    // Сам генератор меша (в мировых координатах)
    Mesh GenerateHandMesh(List<Vector2> points, float width)
    {
        var mesh = new Mesh();
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uvs = new List<Vector2>();

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 dir = (points[i + 1] - points[i]).normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x);
            Vector2 p1 = points[i] + normal * width * 0.5f;
            Vector2 p2 = points[i] - normal * width * 0.5f;
            Vector2 p3 = points[i + 1] + normal * width * 0.5f;
            Vector2 p4 = points[i + 1] - normal * width * 0.5f;

            int idx = vertices.Count;

            // Добавляем вершины
            vertices.Add(p1);
            vertices.Add(p2);
            vertices.Add(p3);
            vertices.Add(p4);

            // Добавляем треугольники
            triangles.Add(idx + 0);
            triangles.Add(idx + 2);
            triangles.Add(idx + 1);

            triangles.Add(idx + 1);
            triangles.Add(idx + 2);
            triangles.Add(idx + 3);

            // UV по длине линии (можно заменить на кастом)
            float u0 = i / (float)(points.Count - 1);
            float u1 = (i + 1) / (float)(points.Count - 1);
            uvs.Add(new Vector2(u0, 0));
            uvs.Add(new Vector2(u0, 1));
            uvs.Add(new Vector2(u1, 0));
            uvs.Add(new Vector2(u1, 1));
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
