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

    [Header("Performance")]
    [Tooltip("Как часто обновлять масштаб (в секундах). 0 = каждый кадр")]
    public float updateInterval = 0.05f; // ~20 раз в секунду вместо 60+

    public float scaleSmoothSpeed = 5f;

    private PixelPerfectCamera ppc;
    private CinemachineBrain brain;

    private float smoothScale = 1f;
    private float updateTimer = 0f;

    // Кэшируем последние записанные значения — не трогаем PPC без нужды
    private int lastResX = -1;
    private int lastResY = -1;

    void Reset() => InitializeComponents();

    void Awake()
    {
        InitializeComponents();

        float aspect = (float)Screen.width / Screen.height;

        // 16:10 vs 16:9
        bool is1610 = Mathf.Abs(aspect - 1.6f) < 0.1f;

        int baseX = is1610 ? Mathf.RoundToInt(320 * 1.5f) : Mathf.RoundToInt(320 * 1.5f);
        int baseY = is1610 ? Mathf.RoundToInt(200 * 1.5f) : Mathf.RoundToInt(180 * 1.5f);

        ppc.refResolutionX = baseX;
        ppc.refResolutionY = baseY;
        baseReferenceResolution = new Vector2Int(baseX, baseY);

        lastResX = baseX;
        lastResY = baseY;
    }

    void InitializeComponents()
    {
        if (ppc == null) ppc = GetComponent<PixelPerfectCamera>();
        if (brain == null) brain = GetComponent<CinemachineBrain>();
    }

    void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) return;
#endif
        if (ppc == null || brain == null) return;

        // Пропускаем кадры — главная оптимизация для слабых устройств
        updateTimer += Time.unscaledDeltaTime;
        if (updateTimer < updateInterval) return;
        updateTimer = 0f;

        float targetOrthoSize = GetBlendedOrthoSize();
        float targetScale = Mathf.Clamp(targetOrthoSize / baseOrthoSize, 1f, maxScale);

        // MoveTowards с учётом прошедшего времени (включая пропущенные кадры)
        smoothScale = Mathf.MoveTowards(
            smoothScale,
            targetScale,
            updateInterval * scaleSmoothSpeed
        );

        int newX = Mathf.RoundToInt(baseReferenceResolution.x * smoothScale);
        int newY = Mathf.RoundToInt(baseReferenceResolution.y * smoothScale);

        // Записываем в PPC только при реальном изменении
        if (newX != lastResX || newY != lastResY)
        {
            ppc.refResolutionX = newX;
            ppc.refResolutionY = newY;
            lastResX = newX;
            lastResY = newY;
        }
    }

    float GetBlendedOrthoSize()
    {
        if (brain == null) return baseOrthoSize;

        var blend = brain.ActiveBlend;
        if (blend != null)
        {
            // Избегаем is-cast с аллокацией — используем as
            var camA = blend.CamA as CinemachineVirtualCamera;
            var camB = blend.CamB as CinemachineVirtualCamera;

            if (camA != null && camB != null)
            {
                return Mathf.Lerp(
                    camA.m_Lens.OrthographicSize,
                    camB.m_Lens.OrthographicSize,
                    blend.BlendWeight
                );
            }
        }

        var activeCam = brain.ActiveVirtualCamera as CinemachineVirtualCamera;
        return activeCam != null ? activeCam.m_Lens.OrthographicSize : baseOrthoSize;
    }
}