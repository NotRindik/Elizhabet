using System;
using UnityEngine;

public class ScreenShateringChange : MonoBehaviour
{
    public float ColorResMult = 4;
    public float ColorResDiv = 0.25f;
    public float DithFactor = 0.09f;
    public float PixelPerUnit = 32;
    
    public DitheringData Data;

    private DitheringData temp;

    private void OnEnable()
    {
        temp = Instantiate(Data);
    }
    private void OnDisable()
    {
        Data.CopyFrom(temp);
    }
    public void Update()
    {
        Data.PixelPerUnit = PixelPerUnit;
        Data.ColorResDiv = ColorResDiv;
        Data.ColorResMult = ColorResMult;
        Data.DithFactor = DithFactor;
    }
}