using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public unsafe class MeshBuilderTest : MonoBehaviour
{
    public MeshFilter filter;
    
    public MeshBuilder  meshBuilder;
    
    
    private void Start()
    {
        filter = GetComponent<MeshFilter>();
        meshBuilder = new MeshBuilder(filter);

        meshBuilder.BuildTriangle()
            .BuildTriangle().Move(new Vector3(5, 0, 0))
            .BuildTriangle().Move(new Vector3(10, 0, 0)) 
            .BuildMesh();
        
    }

    public unsafe void Update()
    {
        meshBuilder.UpdateMesh();
    }

    private void OnDestroy()
    {
        meshBuilder.Dispose();
    }
}
