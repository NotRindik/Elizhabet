using System;
using UnityEngine;
using UnityEngine.Serialization;

public class FogManager : MonoBehaviour
{
    public SpriteRenderer sr;
    public Material fogMaterial;

    public float FogSize;
    public Color FogColor;
    public Vector2 FogSpeed;
    private void Awake()
    {
        sr ??= GetComponent<SpriteRenderer>();
        sr.material = new Material(fogMaterial);
        
        sr.material.SetFloat("_FogSize",FogSize);
        sr.material.SetColor("_FogColor",FogColor);
        sr.material.SetVector("_FogSpeed",FogSpeed);
    }
}
