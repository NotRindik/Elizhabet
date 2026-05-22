using Cinemachine;
using UnityEngine;

public class PixelPerfectPassHelper : MonoBehaviour, ICameraExtension
{
    [SerializeField] private float ppu = 32f;

    [Header("Zoom Snap")]
    [SerializeField] private bool snapOrthographicSize = true;
    
    [Tooltip("Шаг снаппинга в пикселях рендер-буфера.\n" +
             "1  = каждый пиксель (максимум плавности)\n" +
             "2  = каждые 2 пикселя\n" +
             "Аналог zoomSubdivisions, но без дробного рендера")]
    [SerializeField, Range(1, 32)] private int renderPixelStep = 2;

    public int priority;
    public int Priority => priority;
    
    public void Execute(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Body)
            return;

        // 1. Сначала снаппаем размер — прямо в state
        if (snapOrthographicSize)
            SnapOrtho(vcam, ref state); // теперь принимает ref state

        // 2. Потом считаем позицию — уже по снаппнутому size
        SnapPosition(vcam, ref state);
    }

    private void SnapPosition(CinemachineVirtualCameraBase vcam, ref CameraState state)
    {
        if (vcam == null)
            return;

        float targetHeight = Screen.height;

        float unitsPerPixel = 1f / ppu;

        Matrix4x4 W2C = vcam.transform.worldToLocalMatrix;
        Matrix4x4 C2W = vcam.transform.localToWorldMatrix;

        Vector3 worldPos = state.FinalPosition;
        
        Vector3 camSpace =
            W2C.MultiplyPoint3x4(worldPos);
        
        camSpace.x =
            Mathf.Round(camSpace.x / unitsPerPixel) * unitsPerPixel;

        camSpace.y =
            Mathf.Round(camSpace.y / unitsPerPixel) * unitsPerPixel;
        
        Vector3 snappedWorld =
            C2W.MultiplyPoint3x4(camSpace);
        
        state.PositionCorrection =
            snappedWorld - state.FinalPosition;
    }
    
    private void SnapOrtho(CinemachineVirtualCameraBase vcam, ref CameraState state)
    {
        float targetSize = state.Lens.OrthographicSize;
    
        // Желаемая высота рендер-буфера (в пикселях, с плавающей точкой)
        float idealRenderHeight = targetSize * 2f * ppu;
    
        // Снаппаем к кратному renderPixelStep — результат ВСЕГДА целое число
        int renderHeight = Mathf.Max(
            renderPixelStep,
            Mathf.RoundToInt(idealRenderHeight / renderPixelStep) * renderPixelStep);
    
        // OrthoSize строго из целого числа пикселей
        float snappedSize = renderHeight / (ppu * 2f);
    
        LensSettings lens = state.Lens;
        lens.OrthographicSize = snappedSize;
        state.Lens = lens;
    
        if (vcam is CinemachineVirtualCamera cam)
        {
            var camLens = cam.m_Lens;
            camLens.OrthographicSize = snappedSize;
            cam.m_Lens = camLens;
        }
    }
}