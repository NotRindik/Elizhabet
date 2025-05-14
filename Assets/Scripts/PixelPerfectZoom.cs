using Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
[ExecuteAlways]
#endif
[RequireComponent(typeof(PixelPerfectCamera), typeof(CinemachineBrain))]
public class PixelPerfectZoom : MonoBehaviour
{
    public float baseOrthoSize = 5f;
    public Vector2Int baseReferenceResolution = new Vector2Int(384, 216);
    public int maxScale = 10;

    private PixelPerfectCamera ppc;
    private CinemachineBrain brain;

    private float smoothScale = 1f;
    public float scaleSmoothSpeed = 5f;

    void Reset()
    {
        InitializeComponents();
    }

    void Awake()
    {
        InitializeComponents();
        Vector2 nativeResolution = new Vector2(Screen.width, Screen.height);
        float aspect = nativeResolution.x / nativeResolution.y;
        
        Debug.Log(aspect);
        Debug.Log(nativeResolution);
        
        if (Mathf.Abs(aspect - 16f / 10f) < 0.1f)
        {
            ppc.refResolutionX = 320;
            ppc.refResolutionY = 200;
            
            baseReferenceResolution.x = 320;
            baseReferenceResolution.y = 200;
        }
        else
        {
            ppc.refResolutionX = 320;
            ppc.refResolutionY = 180;
            
            baseReferenceResolution.x = 320;
            baseReferenceResolution.y = 180;
        }
    }

    void InitializeComponents()
    {
        if (ppc == null) ppc = GetComponent<PixelPerfectCamera>();
        if (brain == null) brain = GetComponent<CinemachineBrain>();
    }

    void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) return; // отключаем в редакторе
#endif
        if (ppc == null || brain == null)
            return;

        float targetOrthoSize = GetBlendedOrthoSize();
        float targetScale = Mathf.Clamp(targetOrthoSize / baseOrthoSize, 1f, maxScale);

        smoothScale = Mathf.MoveTowards(smoothScale, targetScale, Time.unscaledDeltaTime * scaleSmoothSpeed);

        ppc.refResolutionX = Mathf.RoundToInt(baseReferenceResolution.x * smoothScale);
        ppc.refResolutionY = Mathf.RoundToInt(baseReferenceResolution.y * smoothScale);
    }

    float GetBlendedOrthoSize()
    {
        if (brain == null) return baseOrthoSize;

        var blend = brain.ActiveBlend;
        if (blend != null &&
            blend.CamA is CinemachineVirtualCamera camA &&
            blend.CamB is CinemachineVirtualCamera camB)
        {
            float sizeA = camA.m_Lens.OrthographicSize;
            float sizeB = camB.m_Lens.OrthographicSize;
            return Mathf.Lerp(sizeA, sizeB, blend.BlendWeight);
        }

        if (brain.ActiveVirtualCamera is CinemachineVirtualCamera activeCam)
        {
            return activeCam.m_Lens.OrthographicSize;
        }

        return baseOrthoSize;
    }
}
