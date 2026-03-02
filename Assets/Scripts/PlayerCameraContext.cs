using Cinemachine;
using UnityEngine;

[RequireComponent(typeof(CinemachineVirtualCamera))]
public class PlayerCameraContext : MonoBehaviour
{
    private CinemachineVirtualCamera _vcam;

    private void Start()
    {
        _vcam = GetComponent<CinemachineVirtualCamera>();
        _vcam.Follow = ContextManager.Instance.player.transform;
    }
}
