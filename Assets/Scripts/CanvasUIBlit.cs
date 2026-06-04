using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class CanvasUIBlit : MonoBehaviour
{
    private void Start()
    {
        ContextManager.Instance.OnCameraChange += OnCameraChange;
        ApplyUICamera(ContextManager.Instance.mainCamera);
    }

    private void OnDestroy()
    {
        if (ContextManager.Instance != null)
            ContextManager.Instance.OnCameraChange -= OnCameraChange;
    }

    private void OnCameraChange(Camera camera)
    {
        ApplyUICamera(camera);
    }

    private void ApplyUICamera(Camera mainCamera)
    {
        if (mainCamera == null) return;

        var renderer = mainCamera.GetComponent<PixelPerfectRenderer>();
        if (renderer == null) return;

        var uiCamera = renderer.uiCamera;
        if (uiCamera == null) return;

        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = uiCamera;
    }
}