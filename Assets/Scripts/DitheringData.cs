using System;
using UnityEngine;

[Serializable]
[CreateAssetMenu]
public class DitheringData : ScriptableObject
{
    public float ColorResMult = 4;
    public float ColorResDiv = 0.25f;
    public float DithFactor = 0.0900000036f;
    public float PixelPerUnit = 32;
    
    public void CopyFrom(DitheringData other)
    {
        ColorResMult = other.ColorResMult;
        ColorResDiv = other.ColorResDiv;
        DithFactor = other.DithFactor;
        PixelPerUnit = other.PixelPerUnit;
    }
}