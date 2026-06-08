using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class CableRenderer : MonoBehaviour
{
    [Header("Points")]
    public Transform startPoint;
    public Transform endPoint;

    [Header("Cable")]
    [Min(2)]
    public int segments = 30;

    [Header("Weight")]
    [Range(0f, 1f)]
    public float pressurePoint = 0.5f;

    [Min(0)]
    public float weightForce = 2f;

    [Min(0.01f)]
    public float influenceRadius = 0.25f;

    [Header("Curve")]
    [Min(0.01f)]
    public float smoothness = 2f;

    [FormerlySerializedAs("brokenLinePrefab")]
    [Header("Break")]
    public LineRenderer brokenLine;

    public float breakDuration = 2f;
    public float gravity = 10f;
    public float snapForce = 3f;
    public float followStrength = 8f;

    private LineRenderer line;
    private Coroutine breakRoutine;
    private bool isBroken;

    public Vector3 PressurePosition { get; private set; }

    private void OnEnable()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;

        ResetCable();
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UpdateCable();
#endif
    }

    private void LateUpdate()
    {
        if (Application.isPlaying && !isBroken)
            UpdateCable();
    }

    public void SetPressurePosition(Vector3 worldPosition)
    {
        if (!startPoint || !endPoint)
            return;

        Vector3 dir = endPoint.position - startPoint.position;
        float length = dir.magnitude;

        if (length < 0.001f)
            return;

        float projected = Vector3.Dot(
            worldPosition - startPoint.position,
            dir.normalized
        );

        pressurePoint = Mathf.Clamp01(projected / length);
    }

    private void UpdateCable()
    {
        if (!startPoint || !endPoint)
            return;

        line.positionCount = segments;

        Vector3 start = startPoint.position;
        Vector3 end = endPoint.position;

        PressurePosition = Vector3.Lerp(start, end, pressurePoint);

        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)(segments - 1);

            Vector3 pos = Vector3.Lerp(start, end, t);

            float distance = Mathf.Abs(t - pressurePoint);
            float normalized = Mathf.Clamp01(distance / influenceRadius);

            float influence =
                Mathf.Pow(1f - normalized, smoothness);

            pos += Vector3.down * (weightForce * influence);

            line.SetPosition(i, pos);
        }
    }

    [ContextMenu("Break Cable")]
    public void BreakCable()
    {
        if (!brokenLine)
        {
            Debug.LogError("Broken Line missing!");
            return;
        }

        ResetCable();

        isBroken = true;
        breakRoutine = StartCoroutine(BreakAnimation());
    }

    [ContextMenu("Reset Cable")]
    public void ResetCable()
    {
        isBroken = false;

        if (breakRoutine != null)
        {
            StopCoroutine(breakRoutine);
            breakRoutine = null;
        }

        if (!line)
            line = GetComponent<LineRenderer>();

        line.enabled = true;

        if (brokenLine)
        {
            brokenLine.enabled = false;
            brokenLine.positionCount = 0;
        }

        UpdateCable();
    }

    private IEnumerator BreakAnimation()
{
    Vector3[] original = new Vector3[line.positionCount];
    line.GetPositions(original);

    int breakIndex = Mathf.RoundToInt(
        pressurePoint * (segments - 1)
    );

    Vector3[] left  = new Vector3[breakIndex + 1];
    Vector3[] right = new Vector3[original.Length - breakIndex];

    for (int i = 0; i < left.Length; i++)
        left[i] = original[i];
    for (int i = 0; i < right.Length; i++)
        right[i] = original[breakIndex + i];

    line.positionCount = left.Length;
    line.SetPositions(left);

    brokenLine.enabled = true;
    brokenLine.useWorldSpace = true;
    brokenLine.positionCount = right.Length;
    brokenLine.SetPositions(right);

    Vector3[] leftVelocity  = new Vector3[left.Length];
    Vector3[] rightVelocity = new Vector3[right.Length];

    leftVelocity[left.Length - 1]  = Vector3.left  * snapForce;
    rightVelocity[0]               = Vector3.right * snapForce;

    // Считаем среднюю длину сегмента по всей верёвке
    float totalLength = 0f;
    for (int i = 1; i < original.Length; i++)
        totalLength += Vector3.Distance(original[i], original[i - 1]);
    float segmentLength = (totalLength / (original.Length - 1)) * 1.5f; // запас 50% чтобы не закручивало

    float timer = 0f;

    while (timer < breakDuration)
{
    float dt = Time.deltaTime;
    timer += dt;

    // Затухание усиливается к концу анимации
    float damping = Mathf.Lerp(0.98f, 0.85f, timer / breakDuration);
    // Порог — если скорость совсем маленькая, обнуляем
    float velocityThreshold = 0.01f;

    // ── ЛЕВАЯ часть ──────────────────────────────────────────
    left[0] = startPoint.position;

    for (int i = left.Length - 1; i >= 1; i--)
    {
        leftVelocity[i] += Vector3.down * gravity * dt;
        leftVelocity[i] *= damping;

        if (leftVelocity[i].sqrMagnitude < velocityThreshold * velocityThreshold)
            leftVelocity[i] = Vector3.zero;

        left[i] += leftVelocity[i] * dt;
    }

    for (int i = 1; i < left.Length; i++)
    {
        Vector3 dir = left[i] - left[i - 1];
        float dist  = dir.magnitude;
        if (dist > segmentLength)
        {
            float excess = dist - segmentLength;
            Vector3 correction = dir.normalized * excess * 0.5f;
            left[i] -= correction;
            leftVelocity[i] -= Vector3.Project(leftVelocity[i], dir.normalized) * 0.5f;
        }
    }

    // ── ПРАВАЯ часть ─────────────────────────────────────────
    right[right.Length - 1] = endPoint.position;

    for (int i = 0; i <= right.Length - 2; i++)
    {
        rightVelocity[i] += Vector3.down * gravity * dt;
        rightVelocity[i] *= damping;

        if (rightVelocity[i].sqrMagnitude < velocityThreshold * velocityThreshold)
            rightVelocity[i] = Vector3.zero;

        right[i] += rightVelocity[i] * dt;
    }

    for (int i = right.Length - 2; i >= 0; i--)
    {
        Vector3 dir = right[i] - right[i + 1];
        float dist  = dir.magnitude;
        if (dist > segmentLength)
        {
            float excess = dist - segmentLength;
            Vector3 correction = dir.normalized * excess * 0.5f;
            right[i] -= correction;
            rightVelocity[i] -= Vector3.Project(rightVelocity[i], dir.normalized) * 0.5f;
        }
    }

    line.SetPositions(left);
    brokenLine.SetPositions(right);

    yield return null;
}

    breakRoutine = null;
}

#if UNITY_EDITOR
    private void OnValidate()
    {
        segments = Mathf.Max(segments, 2);

        if (!isBroken)
            UpdateCable();
    }
#endif
}