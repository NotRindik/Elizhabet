using Cinemachine;
using UnityEngine;

public class PixelPerfectPassHelper : CinemachineExtension
{
    [SerializeField] private float ppu = 32f;

    [Header("Zoom Snap")]
    [SerializeField] private bool snapOrthographicSize = true;

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Body)
            return;

        SnapPosition(ref state);

        if (snapOrthographicSize)
            SnapOrtho(vcam);
    }

    private void SnapPosition(ref CameraState state)
    {
        float step = 1f / ppu;

        var pos = state.FinalPosition;

        pos.x = Mathf.Round(pos.x / step) * step;
        pos.y = Mathf.Round(pos.y / step) * step;

        state.PositionCorrection =
            pos - state.FinalPosition;
    }

    private void SnapOrtho(
        CinemachineVirtualCameraBase vcam)
    {
        if (vcam is not CinemachineVirtualCamera cam)
            return;

        LensSettings lens = cam.m_Lens;

        float step = 1f / (ppu * 2f);

        lens.OrthographicSize =
            Mathf.Round(
                lens.OrthographicSize / step)
            * step;

        cam.m_Lens = lens;
    }
}