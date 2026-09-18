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
    private CommandBuffer _compositeCmd;
    private Vector2 subpixelOffsetUV;

    private static readonly int SubpixelOffsetId = Shader.PropertyToID("_SubpixelOffset");

    private bool IsActive => uiCamera != null && targetCamera != null;

    private void OnEnable()
    {
        RebuildMaterial(ref uiBlendMaterial, uiBlendShader);
        RebuildMaterial(ref pixelPerfectMaterial, subPixelShader);
        _compositeCmd = new CommandBuffer { name = "PixelPerfect_Composite" };

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

        if(targetCamera)
        {
            targetCamera.targetTexture = null;
            targetCamera.ResetWorldToCameraMatrix();
            targetCamera.ResetProjectionMatrix();
        }

        if(uiCamera)
        {
            uiCamera.targetTexture = null;
#if UNITY_EDITOR
            uiCamera.gameObject.SetActive(false);
#endif
        }

        ReleaseTexture(ref lowResTexture);
        ReleaseTexture(ref uiTexture);
        SafeDestroy(ref uiBlendMaterial);
        SafeDestroy(ref pixelPerfectMaterial);
    }

    private void Update()
    {
#if UNITY_EDITOR
        UpdateUICameraState();
#endif
    }

    private void OnValidate()
    {
        RebuildMaterial(ref uiBlendMaterial, uiBlendShader);
        RebuildMaterial(ref pixelPerfectMaterial, subPixelShader);

#if UNITY_EDITOR
        UpdateUICameraState();
#endif
    }

    private void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if(!IsActive)
        {
            if(UICamera.Instance != null) uiCamera = UICamera.Instance.uiCamera;
            return;
        }

        if(camera == targetCamera)
        {
            UpdateLowResRT(camera);
            UpdateSubpixelOffset(camera);

            camera.targetTexture = lowResTexture;
            camera.worldToCameraMatrix = CalculateViewMatrix(SnapToPixel(camera.transform.position), camera.transform.rotation);
            if(camera.orthographic) camera.projectionMatrix = CalculateOrthoMatrix(camera);
            return;
        }

        if(camera == uiCamera)
        {
            UpdateUIRT(camera);
            camera.targetTexture = uiTexture;
        }
    }

    private void EndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if(!IsActive) return;

        if(camera == targetCamera)
        {
            camera.targetTexture = null;
            camera.ResetWorldToCameraMatrix();
            camera.ResetProjectionMatrix();
            return;
        }

        if(camera == uiCamera)
        {
            camera.targetTexture = null;
            Composite();
        }
    }

    private Vector3 SnapToPixel(Vector3 pos)
    {
        var pixelSize = 1f / pixelsPerUnit;
        return new Vector3(Mathf.Round(pos.x / pixelSize) * pixelSize, Mathf.Round(pos.y / pixelSize) * pixelSize, pos.z);
    }

    private static Matrix4x4 CalculateViewMatrix(Vector3 pos, Quaternion rot)
    {
        var rotScale = Matrix4x4.Rotate(rot) * Matrix4x4.Scale(new Vector3(1, 1, -1));
        var view = rotScale.transpose;
        view.SetColumn(3, new Vector4(-Vector3.Dot(rotScale.GetColumn(0), pos), -Vector3.Dot(rotScale.GetColumn(1), pos), -Vector3.Dot(rotScale.GetColumn(2), pos), 1f));
        return view;
    }

    private Matrix4x4 CalculateOrthoMatrix(Camera cam)
    {
        var halfW = lowResTexture.width * 0.5f / pixelsPerUnit;
        var halfH = lowResTexture.height * 0.5f / pixelsPerUnit;
        return Matrix4x4.Ortho(-halfW, halfW, -halfH, halfH, cam.nearClipPlane, cam.farClipPlane);
    }

    private void UpdateSubpixelOffset(Camera cam)
    {
        if(lowResTexture == null) return;

        var pixelSize = 1f / pixelsPerUnit;
        var pixelPosX = cam.transform.position.x / pixelSize;
        var pixelPosY = cam.transform.position.y / pixelSize;

        subpixelOffsetUV = new Vector2((Mathf.Round(pixelPosX) - pixelPosX) / lowResTexture.width, (Mathf.Round(pixelPosY) - pixelPosY) / lowResTexture.height);
    }

    private static int MakeEven(float value)
    {
        return Mathf.Max(2, Mathf.RoundToInt(value * 0.5f) * 2);
    }

    private void UpdateLowResRT(Camera cam)
    {
        var heightUnits = cam.orthographic ? cam.orthographicSize * 2f : 2f * perspectiveReferenceDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        var h = MakeEven(heightUnits * pixelsPerUnit);
        var w = MakeEven(h * cam.aspect);

        if(lowResTexture != null && lowResTexture.width == w && lowResTexture.height == h) return;

        if(lowResTexture == null)
        {
            lowResTexture = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR) { filterMode = FilterMode.Point, useMipMap = false, autoGenerateMips = false };
        }
        else
        {
            lowResTexture.Release();
            lowResTexture.width = w;
            lowResTexture.height = h;
        }

        lowResTexture.Create();
    }

    private void UpdateUIRT(Camera cam)
    {
        var w = Mathf.Max(1, cam.pixelWidth);
        var h = Mathf.Max(1, cam.pixelHeight);

        if(uiTexture != null && uiTexture.width == w && uiTexture.height == h) return;

        ReleaseTexture(ref uiTexture);
        uiTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        uiTexture.Create();
    }

    private void Composite()
    {
        if(lowResTexture == null || pixelPerfectMaterial == null) return;

        pixelPerfectMaterial.SetVector(SubpixelOffsetId, subpixelOffsetUV);

        _compositeCmd.Clear();
        _compositeCmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
        _compositeCmd.SetViewport(uiCamera.pixelRect);
        _compositeCmd.Blit(lowResTexture, BuiltinRenderTextureType.CameraTarget, pixelPerfectMaterial);
        if(uiTexture != null && uiBlendMaterial != null) _compositeCmd.Blit(uiTexture, BuiltinRenderTextureType.CameraTarget, uiBlendMaterial);
        Graphics.ExecuteCommandBuffer(_compositeCmd);
    }

    private void RebuildMaterial(ref Material material, Shader shader)
    {
        if(shader == null) return;
        if(material != null && material.shader == shader) return;

        SafeDestroy(ref material);
        material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
    }

#if UNITY_EDITOR
    private void UpdateUICameraState()
    {
        if(uiCamera == null) return;
        if(uiCamera.gameObject.activeSelf != IsActive) uiCamera.gameObject.SetActive(IsActive);
    }
#endif

    private static void ReleaseTexture(ref RenderTexture rt)
    {
        if(rt == null) return;

        rt.Release();
        SafeDestroyObject(rt);
        rt = null;
    }

    private static void SafeDestroy(ref Material material)
    {
        if(material == null) return;

        SafeDestroyObject(material);
        material = null;
    }

    private static void SafeDestroyObject(Object obj)
    {
        if(Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}