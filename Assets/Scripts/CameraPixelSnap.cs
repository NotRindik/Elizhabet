using Cinemachine;
using UnityEngine;

[RequireComponent(typeof(CinemachineBrain))]
public class CameraPixelSnap : MonoBehaviour
{
    [SerializeField] float ppu = 32f;

    void OnEnable()  => CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
    void OnDisable() => CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);

    void OnCameraUpdated(CinemachineBrain brain)
    {
        if (brain.gameObject != gameObject) return;

        float pixelSize = 1f / ppu;
        Vector3 pos = transform.position;
        pos.x = Mathf.Round(pos.x / pixelSize) * pixelSize;
        pos.y = Mathf.Round(pos.y / pixelSize) * pixelSize;
        transform.position = pos;
    }
}