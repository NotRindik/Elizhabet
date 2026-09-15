using UnityEngine;
using TMPro;

namespace ntw.CurvedTextMeshPro
{
    /// <summary>
    /// Base class for drawing a Text Pro text following a particular curve.
    /// </summary>
    [ExecuteInEditMode]
    public abstract class TextProOnACurve : MonoBehaviour
    {
        private TMP_Text m_TextComponent;
        private bool m_forceUpdate;

        private void Awake()
        {
            m_TextComponent = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            m_forceUpdate = true;

            // Подписываемся на нативное событие TMP: оно стреляет при ЛЮБОМ
            // ForceMeshUpdate внутри TextMeshPro — в том числе когда меняется
            // только цвет (например, через твин), а не текст/шрифт/спейсинг.
            // Именно из-за такого скрытого ребилда кривая раньше сбрасывалась.
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(ON_TEXT_CHANGED);
        }

        private void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(ON_TEXT_CHANGED);
        }

        private void ON_TEXT_CHANGED(UnityEngine.Object obj)
        {
            // Событие глобальное для всех TMP-компонентов на сцене,
            // поэтому фильтруем по своему конкретному компоненту.
            if (obj != m_TextComponent)
                return;

            // К этому моменту TMP УЖЕ перестроил mesh (событие стреляет
            // именно после этого), поэтому повторный ForceMeshUpdate тут
            // не нужен — просто применяем кривую к готовым вершинам.
            ApplyCurveToVertices();
        }

        protected void Update()
        {
            // Оставляем ручную проверку как fallback — на случай, если
            // поменяли параметры самой кривой (радиус, дугу, offset и т.д.),
            // что не вызывает TEXT_CHANGED_EVENT у TMP.
            if (!m_forceUpdate && !ParametersHaveChanged())
            {
                return;
            }

            m_forceUpdate = false;

            RebuildTextMesh();
        }

        private void RebuildTextMesh()
        {
            if (m_TextComponent == null)
                return;

            // ForceMeshUpdate() сам вызовет TEXT_CHANGED_EVENT -> ON_TEXT_CHANGED,
            // но это больше не рекурсия: ON_TEXT_CHANGED вызывает только
            // ApplyCurveToVertices() (без повторного ForceMeshUpdate), так что
            // цепочка обрывается на одном "лишнем" вызове ApplyCurveToVertices,
            // а не уходит в бесконечность.
            m_TextComponent.ForceMeshUpdate();

            ApplyCurveToVertices();
        }

        private void ApplyCurveToVertices()
        {
            if (m_TextComponent == null)
                return;

            TMP_TextInfo textInfo = m_TextComponent.textInfo;
            int characterCount = textInfo.characterCount;

            if (characterCount == 0)
                return;

            float boundsMinX = m_TextComponent.bounds.min.x;
            float boundsMaxX = m_TextComponent.bounds.max.x;
            float boundsWidth = boundsMaxX - boundsMinX;

            if (Mathf.Approximately(boundsWidth, 0f))
                return;

            for (int i = 0; i < characterCount; i++)
            {
                TMP_CharacterInfo characterInfo =
                    textInfo.characterInfo[i];

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

                float zeroToOnePos =
                    (charMidBaselinePos.x - boundsMinX) /
                    boundsWidth;

                Matrix4x4 matrix =
                    ComputeTransformationMatrix(
                        charMidBaselinePos,
                        zeroToOnePos,
                        textInfo,
                        i
                    );

                vertices[vertexIndex + 0] =
                    matrix.MultiplyPoint3x4(
                        vertices[vertexIndex + 0]
                    );

                vertices[vertexIndex + 1] =
                    matrix.MultiplyPoint3x4(
                        vertices[vertexIndex + 1]
                    );

                vertices[vertexIndex + 2] =
                    matrix.MultiplyPoint3x4(
                        vertices[vertexIndex + 2]
                    );

                vertices[vertexIndex + 3] =
                    matrix.MultiplyPoint3x4(
                        vertices[vertexIndex + 3]
                    );
            }

            // Важно: curved text изменяет только позиции.
            // Цвета и прочие данные TMP mesh здесь не перезаписываются.
            m_TextComponent.UpdateVertexData(
                TMP_VertexDataUpdateFlags.Vertices
            );
        }

        protected abstract bool ParametersHaveChanged();

        protected abstract Matrix4x4 ComputeTransformationMatrix(
            Vector3 charMidBaselinePos,
            float zeroToOnePos,
            TMP_TextInfo textInfo,
            int charIdx
        );
    }
}