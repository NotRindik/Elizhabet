using System;
using UnityEngine;

public class OutLine : MonoBehaviour
{
    public bool isOutlineEnable;
    public SpriteRenderer sr;
    public Material outlineMat;

    private void Start()
    {
        sr ??= GetComponent<SpriteRenderer>();
        outlineMat = outlineMat == null ? new Material(sr.sharedMaterial) : new Material(outlineMat);
        sr.material = outlineMat;
    }

    public void Enable()
    {
        isOutlineEnable = true;
        outlineMat.SetFloat("_OutlineThickness",1);
    }
    
    public void Disable()
    {
        isOutlineEnable = false;
        outlineMat.SetFloat("_OutlineThickness",0);
    }
}
