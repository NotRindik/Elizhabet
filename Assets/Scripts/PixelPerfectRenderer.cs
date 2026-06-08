using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class PixelPerfectRenderer : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float perspectiveReferenceDistance = 10f;
    [SerializeField] private int pixelsPerUnit = 32;

    [SerializeField] private Shader uiBlendShader;
    [SerializeField] public Camera uiCamera;

    public RenderTexture lowResTexture;
    private RenderTexture uiTexture;
    private Material uiBlendMaterial;

    private int cachedScreenW;
    private int cachedScreenH;
    
    private CommandBuffer _compositeCmd;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        RebuildMaterial();

        _compositeCmd = new CommandBuffer { name = "PixelPerfect_Composite" };
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering   += EndCameraRendering;

#if UNITY_EDITOR
        if (uiCamera) uiCamera.gameObject.SetActive(true);
#endif
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering   -= EndCameraRendering;
        _compositeCmd?.Release();
        _compositeCmd = null;

        if (targetCamera) targetCamera.targetTexture = null;

        if (uiCamera)
        {
            uiCamera.targetTexture = null;
#if UNITY_EDITOR
            uiCamera.gameObject.SetActive(false);
#endif
        }

        ReleaseTexture(ref lowResTexture);
        ReleaseTexture(ref uiTexture);

        DestroyImmediate(uiBlendMaterial); // не Destroy — работает в Editor тоже
        uiBlendMaterial = null;
    }

    private void OnValidate()
    {
        // Шейдер поменяли в инспекторе — пересобрать материал
        RebuildMaterial();
    }

    // ─── Render callbacks ─────────────────────────────────────────────────────

    private void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == targetCamera)
        {
            UpdateLowResRT(camera);
            camera.targetTexture = lowResTexture;
            return;
        }

        if (camera == uiCamera)
        {
            UpdateUIRT();
            camera.targetTexture = uiTexture;
        }
    }

    private void EndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == targetCamera)
        {
            camera.targetTexture = null;
            return;
        }

        if (camera == uiCamera)
        {
            camera.targetTexture = null;
            Composite();
        }
    }

    // ─── RT management ────────────────────────────────────────────────────────

    private void UpdateLowResRT(Camera cam)
    {
        int targetHeight;

        if (cam.orthographic)
        {
            targetHeight = Mathf.RoundToInt(cam.orthographicSize * 2f * pixelsPerUnit);
        }
        else
        {
            float frustumHeight = 2f * perspectiveReferenceDistance *
                                  Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            targetHeight = Mathf.RoundToInt(frustumHeight * pixelsPerUnit);
        }

        // cam.aspect точнее, чем Screen.width/height (учитывает viewport rect)
        int targetWidth = Mathf.RoundToInt(targetHeight * cam.aspect);

        if (lowResTexture != null &&
            lowResTexture.width  == targetWidth &&
            lowResTexture.height == targetHeight)
            return;

        ReleaseTexture(ref lowResTexture);

        lowResTexture = new RenderTexture(targetWidth, targetHeight, 24, RenderTextureFormat.Default)
        {
            filterMode       = FilterMode.Point,
            useMipMap        = false,
            autoGenerateMips = false
        };
        lowResTexture.Create();
    }

    private void UpdateUIRT()
    {
        int w = Screen.width;
        int h = Screen.height;

        if (uiTexture != null && cachedScreenW == w && cachedScreenH == h)
            return;

        ReleaseTexture(ref uiTexture);

        cachedScreenW = w;
        cachedScreenH = h;

        uiTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        uiTexture.Create();
    }

    // ─── Compositing ──────────────────────────────────────────────────────────

    private void Composite()
    {
        _compositeCmd.Clear();
        _compositeCmd.Blit(lowResTexture, BuiltinRenderTextureType.CameraTarget);
        if (uiTexture != null && uiBlendMaterial != null)
            _compositeCmd.Blit(uiTexture, BuiltinRenderTextureType.CameraTarget, uiBlendMaterial);
    
        Graphics.ExecuteCommandBuffer(_compositeCmd);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private void RebuildMaterial()
    {
        if (uiBlendShader == null) return;

        if (uiBlendMaterial != null && uiBlendMaterial.shader == uiBlendShader)
            return; // уже актуальный

        DestroyImmediate(uiBlendMaterial);
        uiBlendMaterial = new Material(uiBlendShader) { hideFlags = HideFlags.HideAndDontSave };
    }

    private static void ReleaseTexture(ref RenderTexture rt)
    {
        if (rt == null) return;
        rt.Release();
        rt = null;
    }
}