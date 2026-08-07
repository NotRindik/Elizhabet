using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Light2D))]
public class GlobalLightRegister : MonoBehaviour
{
    public void Awake()
    {
        ContextManager.Instance.RegisterGlobalLight(GetComponent<Light2D>());
    }
}
