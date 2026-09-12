using System;
using System.Collections.Generic;
using DG.Tweening;
using Unity.Collections;
using UnityEngine;

public struct FigureRuntime
{
    public int vertexStart;
    public int vertexCount;
    public Color color;
    public float depth;
 
    public static FigureRuntime Default => new FigureRuntime { vertexStart = 0, vertexCount = 0, color = Color.white,depth = 0.05f};
}



public unsafe struct FigureData : IDisposable
{
    public NativeList<Vector3> vertices;
    public NativeList<int> triangles;
    public NativeList<Vector2> uvs;

    public TransformData* transform;
    public FigureRuntime* MeshData;

    public void Dispose()
    {
        if (vertices.IsCreated)
            vertices.Dispose();

        if (triangles.IsCreated)
            triangles.Dispose();

        if (uvs.IsCreated)
            uvs.Dispose();

        std.Unsafe.Free(transform);
        std.Unsafe.Free(MeshData);
    }
}

public unsafe class MeshBuilder : IDisposable
{
    private NativeList<FigureData> _figures = new NativeList<FigureData>(50, Allocator.Persistent);
    private Mesh _mesh;

    private TransformData* _transform;

    private FigureData* _currentFigure;

    public TransformData* Transform => _transform;
    
    private readonly List<Texture> _figureTextures = new List<Texture>();
    private readonly Dictionary<Texture, Material> _materialCache = new Dictionary<Texture, Material>();
    private MeshRenderer _meshRenderer;
    


    
    public MeshBuilder(MeshFilter filter)
    {
        _transform = std.Unsafe.MallocData(TransformData.Default);
        _currentFigure = std.Unsafe.Malloc<FigureData>();

        _mesh = new Mesh();
        _mesh.name = "Runtime Mesh";

        filter.sharedMesh = _mesh;
    }

    public void UpdateMesh()
    {
        var vertices = _mesh.vertices;
        var colors = _mesh.colors;
 
        if (colors.Length != vertices.Length)
            colors = new Color[vertices.Length];
 
        for (int i = 0; i < _figures.Length; i++)
        {
            var figure = _figures[i];
            var matrix = figure.transform->WorldMatrix;
            var tint = figure.MeshData->color;
 
            int start = figure.MeshData->vertexStart;
            int count = figure.MeshData->vertexCount;
 
            for (int j = 0; j < count; j++)
            {
                vertices[start + j] = matrix.MultiplyPoint3x4(figure.vertices[j]);
                colors[start + j] = tint;
            }
        }
 
        _mesh.vertices = vertices;
        _mesh.colors = colors;
        _mesh.RecalculateBounds();
    }
 
    private Material GetOrCreateMaterial(Texture texture)
    {
        if (_materialCache.TryGetValue(texture, out var mat))
            return mat;
 
        mat = new Material(Shader.Find("Custom/MagicCircleGlow"))
        {
            mainTexture = texture
        };
        
        _materialCache[texture] = mat;
        return mat;
    }

    public void Dispose()
    {
        for (int i = 0; i < _figures.Length; i++)
        {
            FigureData figure = _figures[i];
            figure.Dispose();
        }

        _figures.Dispose();

        if (_mesh != null)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(_mesh);
            else
            {
                UnityEngine.Object.DestroyImmediate(_mesh);
            }
            _mesh = null;
        }

        std.Unsafe.Free(_transform);
        std.Unsafe.Free(_currentFigure);
    }
    
    public MeshBuilder BuildSprite(Sprite sprite)
    {
        var spriteVerts = sprite.vertices;
        var spriteUVs = sprite.uv;
        var spriteTris = sprite.triangles;
 
        var vertices = new NativeList<Vector3>(spriteVerts.Length, Allocator.Persistent);
        var triangles = new NativeList<int>(spriteTris.Length, Allocator.Persistent);
        var uvs = new NativeList<Vector2>(spriteUVs.Length, Allocator.Persistent);
 
        for (int i = 0; i < spriteVerts.Length; i++)
        {
            vertices.Add(new Vector3(spriteVerts[i].x, spriteVerts[i].y, 0f));
            uvs.Add(spriteUVs[i]);
        }
 
        for (int i = 0; i < spriteTris.Length; i += 3)
        {
            triangles.Add(spriteTris[i]);
            triangles.Add(spriteTris[i + 1]);
            triangles.Add(spriteTris[i + 2]);
        }
 
        return BuildFigure(vertices, triangles, uvs, sprite.texture);
    }

    
    public MeshBuilder BuildTriangle()
    {
        var vertices = new NativeList<Vector3>(3, Allocator.Persistent)
        {
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0)
        };
        var triangles = new NativeList<int>(3, Allocator.Persistent) { 0, 2, 1 };
        var uvs = new NativeList<Vector2>(3, Allocator.Persistent)
        {
            new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(1f, 0f)
        };

        return BuildFigure(vertices, triangles, uvs);
    }
    
    public MeshBuilder BuildCircle(float radius, int segments = 64)
    {
        return BuildArc(radius, 0f, 360f, segments, filled: true);
    }

    public MeshBuilder BuildRing(float innerRadius, float outerRadius, int segments = 64)
    {
        return BuildRingArc(innerRadius, outerRadius, 0f, 360f, segments);
    }
    
    public MeshBuilder BuildArc(float radius, float startAngle, float endAngle, int segments = 64, bool filled = true)
    {
        int capacity = segments + 2;
        var vertices = new NativeList<Vector3>(capacity, Allocator.Persistent);
        var triangles = new NativeList<int>(capacity * 3, Allocator.Persistent);
        var uvs = new NativeList<Vector2>(capacity, Allocator.Persistent);

        int centerIndex = -1;
        if (filled)
        {
            centerIndex = vertices.Length;
            vertices.Add(Vector3.zero);
            uvs.Add(new Vector2(0.5f, 0.5f));
        }

        float totalAngle = endAngle - startAngle;
        int ringStart = vertices.Length;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = (startAngle + totalAngle * t) * Mathf.Deg2Rad;
            vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            uvs.Add(new Vector2(t, 1f));

            if (filled && i > 0)
            {
                triangles.Add(centerIndex);
                triangles.Add(ringStart + i);
                triangles.Add(ringStart + i - 1);
            }
        }

        return BuildFigure(vertices, triangles, uvs);
    }
    
    public MeshBuilder BuildRingArc(float innerRadius, float outerRadius, float startAngle, float endAngle, int segments = 64)
    {
        int capacity = (segments + 1) * 2;
        var vertices = new NativeList<Vector3>(capacity, Allocator.Persistent);
        var triangles = new NativeList<int>(segments * 6, Allocator.Persistent);
        var uvs = new NativeList<Vector2>(capacity, Allocator.Persistent);

        float totalAngle = endAngle - startAngle;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = (startAngle + totalAngle * t) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            vertices.Add(new Vector3(dir.x * innerRadius, dir.y * innerRadius, 0f));
            vertices.Add(new Vector3(dir.x * outerRadius, dir.y * outerRadius, 0f));

            uvs.Add(new Vector2(t, 0f));
            uvs.Add(new Vector2(t, 1f));

            if (i > 0)
            {
                int a = (i - 1) * 2;
                int b = i * 2;

                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(a + 1);

                triangles.Add(a + 1);
                triangles.Add(b);
                triangles.Add(b + 1);
            }
        }

        return BuildFigure(vertices, triangles, uvs);
    }
    
    public MeshBuilder BuildPolygon(IReadOnlyList<Vector2> points)
    {
        var vertices = new NativeList<Vector3>(points.Count, Allocator.Persistent);
        var triangles = new NativeList<int>((points.Count - 2) * 3, Allocator.Persistent);
        var uvs = new NativeList<Vector2>(points.Count, Allocator.Persistent);

        for (int i = 0; i < points.Count; i++)
        {
            vertices.Add(new Vector3(points[i].x, points[i].y, 0f));
            uvs.Add(Vector2.zero);
        }

        for (int i = 1; i < points.Count - 1; i++)
        {
            triangles.Add(0);
            triangles.Add(i + 1);
            triangles.Add(i);
        }

        return BuildFigure(vertices, triangles, uvs);
    }

    public MeshBuilder BuildRegularPolygon(float radius, int sides, float rotationDegrees = 0f)
    {
        var points = new List<Vector2>(sides);
        for (int i = 0; i < sides; i++)
        {
            float angle = (rotationDegrees + i * 360f / sides) * Mathf.Deg2Rad;
            points.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
        return BuildPolygon(points);
    }
    
    public MeshBuilder BuildStar(float outerRadius, float innerRadius, int points = 5, float rotationDegrees = -90f)
    {
        int totalPoints = points * 2;

        var vertices = new NativeList<Vector3>(totalPoints + 1, Allocator.Persistent);
        var triangles = new NativeList<int>(totalPoints * 3, Allocator.Persistent);
        var uvs = new NativeList<Vector2>(totalPoints + 1, Allocator.Persistent);

        vertices.Add(Vector3.zero);
        uvs.Add(new Vector2(0.5f, 0.5f));

        for (int i = 0; i < totalPoints; i++)
        {
            float r = (i % 2 == 0) ? outerRadius : innerRadius;
            float angle = (rotationDegrees + i * 360f / totalPoints) * Mathf.Deg2Rad;
            vertices.Add(new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f));
            uvs.Add(new Vector2(i / (float)totalPoints, 1f));
        }

        for (int i = 0; i < totalPoints; i++)
        {
            int current = 1 + i;
            int next = 1 + (i + 1) % totalPoints;

            triangles.Add(0);
            triangles.Add(next);
            triangles.Add(current);
        }

        return BuildFigure(vertices, triangles, uvs);
    }
    

    public MeshBuilder BuildFigure(in NativeList<Vector3> vertices, in NativeList<int> triangles, in NativeList<Vector2> uvs, Texture texture = null)
    {
        
        var figure = new FigureData
        {
            vertices = vertices,
            triangles = triangles,
            uvs = uvs,
            transform = std.Unsafe.MallocData(TransformData.Default),
            MeshData = std.Unsafe.MallocData(FigureRuntime.Default),
        };
        AddDepth(vertices, triangles, uvs, figure.MeshData->depth);
        
        figure.transform->parent = _transform;
        figure.transform->pivot = Vector3.zero;

        _figures.Add(figure);
        _figureTextures.Add(texture != null ? texture : Texture2D.whiteTexture);
        *_currentFigure = figure;

        return this;
    }
    private static void AddDepth(NativeList<Vector3> vertices, NativeList<int> triangles, NativeList<Vector2> uvs, float depth)
    {
        if (depth <= 0f || vertices.Length == 0)
            return;

        int originalVertexCount = vertices.Length;
        int originalTriangleCount = triangles.Length;

        float halfDepth = depth * 0.5f;
            
        for (int i = 0; i < originalVertexCount; i++)
        {
            Vector3 vertex = vertices[i];

            vertex.z = -halfDepth;
            vertices[i] = vertex;

            vertex.z = halfDepth;
            vertices.Add(vertex);

            uvs.Add(uvs[i]);
        }
            
        for (int i = 0; i < originalTriangleCount; i += 3)
        {
            int a = triangles[i];
            int b = triangles[i + 1];
            int c = triangles[i + 2];

            triangles.Add(c + originalVertexCount);
            triangles.Add(b + originalVertexCount);
            triangles.Add(a + originalVertexCount);
        }
            
        for (int i = 0; i < originalTriangleCount; i += 3)
        {
            AddSide(triangles[i], triangles[i + 1], originalVertexCount, triangles);

            AddSide(triangles[i + 1], triangles[i + 2], originalVertexCount, triangles);

            AddSide(triangles[i + 2], triangles[i], originalVertexCount, triangles);
        }
    }
        
    private static void AddSide(int a, int b, int vertexOffset, NativeList<int> triangles)
    {
        int aBack = a + vertexOffset;
        int bBack = b + vertexOffset;

        triangles.Add(a);
        triangles.Add(aBack);
        triangles.Add(b);

        triangles.Add(b);
        triangles.Add(aBack);
        triangles.Add(bBack);
    }
    public void BuildMesh()
    {
        int vertexCount = 0;
        var groups = new Dictionary<Texture, List<int>>();
 
        for (int i = 0; i < _figures.Length; i++)
        {
            vertexCount += _figures[i].vertices.Length;
 
            var tex = _figureTextures[i];
            if (!groups.TryGetValue(tex, out var list))
            {
                list = new List<int>();
                groups[tex] = list;
            }
            list.Add(i);
        }
 
        var vertices = new Vector3[vertexCount];
        var uvs = new Vector2[vertexCount];
        var colors = new Color[vertexCount];
 
        int vertexOffset = 0;
        for (int i = 0; i < _figures.Length; i++)
        {
            var figure = _figures[i];
            var matrix = figure.transform->WorldMatrix;
            var tint = figure.MeshData->color;
 
            for (int j = 0; j < figure.vertices.Length; j++)
            {
                vertices[vertexOffset + j] = matrix.MultiplyPoint3x4(figure.vertices[j]);
                uvs[vertexOffset + j] = figure.uvs[j];
                colors[vertexOffset + j] = tint;
            }
 
            figure.MeshData->vertexStart = vertexOffset;
            figure.MeshData->vertexCount = figure.vertices.Length;
            vertexOffset += figure.vertices.Length;
        }
 
        _mesh.Clear();
        _mesh.subMeshCount = groups.Count;
        _mesh.vertices = vertices;
        _mesh.uv = uvs;
        _mesh.colors = colors;
 
        var materials = new Material[groups.Count];
        int submeshIndex = 0;
 
        foreach (var kv in groups)
        {
            var subTriangles = new List<int>();
            foreach (int figureIndex in kv.Value)
            {
                var figure = _figures[figureIndex];
                int offset = figure.MeshData->vertexStart;
 
                for (int j = 0; j < figure.triangles.Length; j++)
                    subTriangles.Add(figure.triangles[j] + offset);
            }
 
            _mesh.SetTriangles(subTriangles, submeshIndex);
            materials[submeshIndex] = GetOrCreateMaterial(kv.Key);
            submeshIndex++;
        }
 
        if (_meshRenderer != null)
            _meshRenderer.sharedMaterials = materials;
 
        _mesh.RecalculateBounds();
        _mesh.RecalculateNormals();
    }

    
    public void SetTransformDataPtr(ref TransformData* ptr) => ptr = _currentFigure->transform;
    public void SetRuntimeDataPtr(ref FigureRuntime* ptr) => ptr = _currentFigure->MeshData;


    public MeshBuilder Move(Vector3 position)
    {
        _currentFigure->transform->localPosition = position;
        return this;
    }

    public MeshBuilder Rotate(Vector3 rotation)
    {
        _currentFigure->transform->localRotation =
            Quaternion.Euler(rotation);

        return this;
    }

    public MeshBuilder Scale(Vector3 scale)
    {
        _currentFigure->transform->localScale = scale;
        return this;
    }
    
    public MeshBuilder Pivot(Vector3 pivot)
    {
        _currentFigure->transform->pivot = pivot;
        return this;
    }
}


public unsafe struct TransformData
{
    private Vector3 _localPosition;
    private Quaternion _localRotation;
    private Vector3 _localScale;

    private Vector3 _localPivot;

    public Vector3 pivot
    {
        get => _localPivot;
        set => _localPivot = value;
    }

    private TransformData* _parent;

    public TransformData* parent
    {
        get => _parent;
        set => _parent = value;
    }

    public Vector3 localPosition
    {
        get => _localPosition;
        set => _localPosition = value;
    }

    public Quaternion localRotation
    {
        get => _localRotation;
        set => _localRotation = value;
    }

    public Vector3 localEulerAngles
    {
        get => _localRotation.eulerAngles;
        set => _localRotation = Quaternion.Euler(value);
    }

    public Vector3 localScale
    {
        get => _localScale;
        set => _localScale = value;
    }

    public Vector3 position
    {
        get => _parent == null
            ? _localPosition
            : _parent->LocalToWorldPoint(_localPosition);

        set => _localPosition = _parent == null
            ? value
            : _parent->WorldToLocalPoint(value);
    }

    public Quaternion rotation
    {
        get => _parent == null
            ? _localRotation
            : _parent->rotation * _localRotation;

        set => _localRotation = _parent == null
            ? value
            : Quaternion.Inverse(_parent->rotation) * value;
    }

    public Vector3 lossyScale
    {
        get => _parent == null
            ? _localScale
            : Vector3.Scale(_parent->lossyScale, _localScale);
    }

    public Matrix4x4 LocalMatrix
    {
        get
        {
            return Matrix4x4.Translate(_localPosition)
                   * Matrix4x4.Rotate(_localRotation)
                   * Matrix4x4.Translate(_localPivot)
                   * Matrix4x4.Scale(_localScale)
                   * Matrix4x4.Translate(-_localPivot);
        }
    }

    public Matrix4x4 WorldMatrix
    {
        get => _parent == null
            ? LocalMatrix
            : _parent->WorldMatrix * LocalMatrix;
    }

    public Vector3 LocalToWorldPoint(Vector3 point)
    {
        return WorldMatrix.MultiplyPoint3x4(point);
    }

    public Vector3 WorldToLocalPoint(Vector3 point)
    {
        return WorldMatrix.inverse.MultiplyPoint3x4(point);
    }

    public Vector3 LocalToWorldVector(Vector3 vector)
    {
        return WorldMatrix.MultiplyVector(vector);
    }

    public Vector3 WorldToLocalVector(Vector3 vector)
    {
        return WorldMatrix.inverse.MultiplyVector(vector);
    }

    public Vector3 LocalToWorldDirection(Vector3 direction)
    {
        return LocalToWorldVector(direction).normalized;
    }

    public Vector3 WorldToLocalDirection(Vector3 direction)
    {
        return WorldToLocalVector(direction).normalized;
    }

    public static TransformData Default => new TransformData
    {
        position = Vector3.zero,
        rotation = Quaternion.identity,
        localScale = Vector3.one,
    };
}