using System;
using UnityEngine;

[ExecuteAlways]
public class Funikuler : MonoBehaviour
{
    public CableRenderer CableRenderer;
    public Rigidbody2D rb;

    
    private void Update()
    {
        if(CableRenderer)
            rb.position = CableRenderer.PressurePosition;
        else
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
        }
    }
}
