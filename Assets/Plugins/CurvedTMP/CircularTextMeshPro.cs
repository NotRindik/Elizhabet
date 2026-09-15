using UnityEngine;
using TMPro;
using Sirenix.OdinInspector;

[ExecuteInEditMode]
[RequireComponent(typeof(TMP_Text))]
public class CircularTextMeshPro : MonoBehaviour
{
    private TMP_Text m_TextComponent;

    [SerializeField, HideInInspector]
    private float m_radius = 10.0f;

    [ShowInInspector]
    [Tooltip("The radius of the text circle arc")]
    public float Radius
    {
        get => m_radius;
        set
        {
            if (Mathf.Approximately(m_radius, value))
                return;

            m_radius = value;
            OnCurvePropertyChanged();
        }
    }

    private void Awake()
    {
        m_TextComponent = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (m_TextComponent == null)
            m_TextComponent = GetComponent<TMP_Text>();

        m_TextComponent.OnPreRenderText += UpdateTextCurve;
        OnCurvePropertyChanged();
    }

    private void OnDisable()
    {
        if (m_TextComponent != null)
            m_TextComponent.OnPreRenderText -= UpdateTextCurve;
    }

    private void OnCurvePropertyChanged()
    {
        m_TextComponent.SetVerticesDirty();
    }

    private void UpdateTextCurve(TMP_TextInfo textInfo)
    {
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo characterInfo = textInfo.characterInfo[i];

            if (!characterInfo.isVisible)
                continue;

            int vertexIndex = characterInfo.vertexIndex;
            int materialIndex = characterInfo.materialReferenceIndex;

            Vector3[] vertices =
                textInfo.meshInfo[materialIndex].vertices;

            Vector3 charMidBaselinePos = new Vector2(
                (vertices[vertexIndex + 0].x +
                 vertices[vertexIndex + 2].x) * 0.5f,
                characterInfo.baseLine
            );

            vertices[vertexIndex + 0] -= charMidBaselinePos;
            vertices[vertexIndex + 1] -= charMidBaselinePos;
            vertices[vertexIndex + 2] -= charMidBaselinePos;
            vertices[vertexIndex + 3] -= charMidBaselinePos;

            Matrix4x4 matrix =
                ComputeTransformationMatrix(
                    charMidBaselinePos,
                    textInfo,
                    i
                );

            vertices[vertexIndex + 0] =
                matrix.MultiplyPoint3x4(vertices[vertexIndex + 0]);

            vertices[vertexIndex + 1] =
                matrix.MultiplyPoint3x4(vertices[vertexIndex + 1]);

            vertices[vertexIndex + 2] =
                matrix.MultiplyPoint3x4(vertices[vertexIndex + 2]);

            vertices[vertexIndex + 3] =
                matrix.MultiplyPoint3x4(vertices[vertexIndex + 3]);
        }
    }

    private Matrix4x4 ComputeTransformationMatrix(
        Vector3 charMidBaselinePos,
        TMP_TextInfo textInfo,
        int charIdx)
    {
        int lineNumber =
            textInfo.characterInfo[charIdx].lineNumber;

        float radiusForThisLine =
            m_radius +
            textInfo.lineInfo[lineNumber].baseline;

        float circumference =
            2f * radiusForThisLine * Mathf.PI;

        float angle =
            (
                (charMidBaselinePos.x / circumference - 0.5f) * 360f
                + 90f
            ) * Mathf.Deg2Rad;

        float x0 = Mathf.Cos(angle);
        float y0 = Mathf.Sin(angle);

        Vector2 newMidBaselinePos =
            new Vector2(
                x0 * radiusForThisLine,
                -y0 * radiusForThisLine
            );

        float rotationAngle =
            -Mathf.Atan2(y0, x0) * Mathf.Rad2Deg - 90f;

        return Matrix4x4.TRS(
            new Vector3(
                newMidBaselinePos.x,
                newMidBaselinePos.y,
                0f
            ),
            Quaternion.AngleAxis(
                rotationAngle,
                Vector3.forward
            ),
            Vector3.one
        );
    }
}