using System;
using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class CanvasUIBlit : MonoBehaviour
{
    
    private void Start()
    {
        if(UICamera.Instance)
            ApplyUICamera(UICamera.Instance);
        
        UICamera.OnInited += ApplyUICamera;
    }

    private void OnDestroy()
    {
        UICamera.OnInited -= ApplyUICamera;
    }

    private void ApplyUICamera(UICamera mainCamera)
    {
        var uiCamera = mainCamera.uiCamera;
        if (uiCamera == null) return;

        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.sortingLayerName = "UI";
        canvas.worldCamera = uiCamera;
    }
}