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
    [SerializeField] private Shader subPixelShader;
    [SerializeField] public Camera uiCamera;

    public RenderTexture lowResTexture;
    private RenderTexture uiTexture;
    private Material uiBlendMaterial;
    private Material pixelPerfectMaterial;

    private int cachedScreenW;
    private int cachedScreenH;

    private CommandBuffer _compositeCmd;

    private Vector2 subpixelOffsetUV;

    private bool IsActive =>
        uiCamera != null &&
        targetCamera != null;

    private void OnEnable()
    {
        RebuildMaterial(ref uiBlendMaterial,uiBlendShader);
        RebuildMaterial(ref pixelPerfectMaterial,subPixelShader);
        _compositeCmd = new CommandBuffer
        {
            name = "PixelPerfect_Composite"
        };

        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;

#if UNITY_EDITOR
        UpdateUICameraState();
#endif
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;

        _compositeCmd?.Release();
        _compositeCmd = null;

        if (targetCamera)
            targetCamera.targetTexture = null;

        if (uiCamera)
        {
            uiCamera.targetTexture = null;

#if UNITY_EDITOR
            uiCamera.gameObject.SetActive(false);
#endif
        }

        ReleaseTexture(ref lowResTexture);
        ReleaseTexture(ref uiTexture);

        DestroyImmediate(uiBlendMaterial);
        uiBlendMaterial = null;
    }

    private void Update()
    {
#if UNITY_EDITOR
        UpdateUICameraState();
#endif
    }

    private void OnValidate()
    {
        RebuildMaterial(ref uiBlendMaterial,uiBlendShader);
        RebuildMaterial(ref pixelPerfectMaterial,subPixelShader);

#if UNITY_EDITOR
        UpdateUICameraState();
#endif
    }

    private void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (!IsActive)
        {
            if(UICamera.Instance != null)
                uiCamera = UICamera.Instance.uiCamera;
            return;
        }

        if (camera == targetCamera)
        {
            UpdateLowResRT(camera);
            UpdateSubpixelOffset(camera);

            Vector3 snappedPos = SnapToPixel(camera.transform.position);
            camera.worldToCameraMatrix = CalculateViewMatrix(snappedPos, camera.transform.rotation);

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
        if (!IsActive)
            return;

        if (camera == targetCamera)
        {
            camera.targetTexture = null;
            camera.ResetWorldToCameraMatrix();
            return;
        }

        if (camera == uiCamera)
        {
            camera.targetTexture = null;
            Composite();
        }
    }

    private Vector3 SnapToPixel(Vector3 pos)
    {
        float pixelSize = 1f / pixelsPerUnit;

        return new Vector3(
            Mathf.Round(pos.x / pixelSize) * pixelSize,
            Mathf.Round(pos.y / pixelSize) * pixelSize,
            pos.z
        );
    }

    private static Matrix4x4 CalculateViewMatrix(Vector3 pos, Quaternion rot)
    {
        return Matrix4x4.TRS(pos, rot, new Vector3(1, 1, -1)).inverse;
    }
    
    private void UpdateSubpixelOffset(Camera cam)
    {
        if (lowResTexture == null)
            return;

        float pixelSize = 1f / pixelsPerUnit;

        Vector3 camPos = cam.transform.position;

        float pixelPosX = camPos.x / pixelSize;
        float pixelPosY = camPos.y / pixelSize;

        float subpixelOffsetPixelsX = Mathf.Round(pixelPosX) - pixelPosX;
        float subpixelOffsetPixelsY = Mathf.Round(pixelPosY) - pixelPosY;
        
        subpixelOffsetUV = new Vector2(
            subpixelOffsetPixelsX / lowResTexture.width,
            subpixelOffsetPixelsY / lowResTexture.height
        );
    }
    
    private void UpdateLowResRT(Camera cam)
    {
        int targetHeight;

        if (cam.orthographic)
        {
            targetHeight =
                Mathf.RoundToInt(
                    cam.orthographicSize * 2f * pixelsPerUnit);
        }
        else
        {
            float frustumHeight =
                2f *
                perspectiveReferenceDistance *
                Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

            targetHeight =
                Mathf.RoundToInt(
                    frustumHeight * pixelsPerUnit);
        }

        int targetWidth =
            Mathf.RoundToInt(
                targetHeight * cam.aspect);

        if (lowResTexture != null &&
            lowResTexture.width == targetWidth &&
            lowResTexture.height == targetHeight)
            return;

        ReleaseTexture(ref lowResTexture);

        lowResTexture =
            new RenderTexture(
                targetWidth,
                targetHeight,
                24,
                RenderTextureFormat.Default)
            {
                filterMode = FilterMode.Point,
                useMipMap = false,
                autoGenerateMips = false
            };

        lowResTexture.Create();
    }

    private void UpdateUIRT()
    {
        int w = Screen.width;
        int h = Screen.height;

        if (uiTexture != null &&
            cachedScreenW == w &&
            cachedScreenH == h)
            return;

        ReleaseTexture(ref uiTexture);

        cachedScreenW = w;
        cachedScreenH = h;

        uiTexture =
            new RenderTexture(
                w,
                h,
                24,
                RenderTextureFormat.ARGB32);

        uiTexture.Create();
    }

    private void Composite()
    {
        if (lowResTexture == null)
            return;

        _compositeCmd.Clear();

        pixelPerfectMaterial.SetVector("_SubpixelOffset", subpixelOffsetUV);

        _compositeCmd.Blit(
            lowResTexture,
            BuiltinRenderTextureType.CameraTarget,
            pixelPerfectMaterial
        );

        if (uiTexture != null &&
            uiBlendMaterial != null)
        {
            _compositeCmd.Blit(
                uiTexture,
                BuiltinRenderTextureType.CameraTarget,
                uiBlendMaterial);
        }

        Graphics.ExecuteCommandBuffer(_compositeCmd);
    }

    private void RebuildMaterial(ref Material material , Shader shader)
    {
        if (shader == null)
            return;

        if (material != null &&
            material.shader == shader)
            return;

        DestroyImmediate(material);

        material =
            new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
    }

#if UNITY_EDITOR
    private void UpdateUICameraState()
    {
        if (uiCamera == null)
            return;

        uiCamera.gameObject.SetActive(IsActive);
    }
#endif

    private static void ReleaseTexture(ref RenderTexture rt)
    {
        if (rt == null)
            return;

        rt.Release();
        rt = null;
    }
}