using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

[ExecuteAlways]
public class ResizeByText : SerializedMonoBehaviour
{
    public enum ResizeAxis { Horizontal, Vertical, Both }

    [System.Flags]
    public enum GrowDirection
    {
        Left   = 1 << 0,
        Right  = 1 << 1,
        Up     = 1 << 2,
        Down   = 1 << 3,
    }

    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private RectTransform target;

    [SerializeField] private ResizeAxis axis = ResizeAxis.Both;
    
    [SerializeField] private GrowDirection growDirection = GrowDirection.Right | GrowDirection.Down;
    
    [SerializeField] private Vector2 padding = new Vector2(20f, 10f);
    [SerializeField] private Vector2 minSize = new Vector2(50f,  30f);
    [SerializeField] private Vector2 maxSize = new Vector2(500f, 300f);

    private string _lastText;
    private Vector2 _lastSize;

    private void Update()
    {
        if (label == null || target == null) return;
#if UNITY_EDITOR
        Apply();
#else
        if (label.text != _lastText) Apply();
#endif
    }

    [Button]
    public void Apply()
    {
        if (label == null || target == null) return;

        label.ForceMeshUpdate();

        Vector2 textSize = label.GetRenderedValues(onlyVisibleCharacters: false);
        Vector2 desired  = textSize + padding;
        Vector2 next     = target.sizeDelta;
        Vector2 pivot    = target.pivot;
        Vector2 newPivot = pivot;

        if (axis == ResizeAxis.Horizontal || axis == ResizeAxis.Both)
        {
            next.x = Mathf.Clamp(desired.x, minSize.x, maxSize.x);

            bool left  = (growDirection & GrowDirection.Left)  != 0;
            bool right = (growDirection & GrowDirection.Right) != 0;

            newPivot.x = left && right ? 0.5f :
                         left          ? 1f   :
                                         0f;
        }

        if (axis == ResizeAxis.Vertical || axis == ResizeAxis.Both)
        {
            next.y = Mathf.Clamp(desired.y, minSize.y, maxSize.y);

            bool up   = (growDirection & GrowDirection.Up)   != 0;
            bool down = (growDirection & GrowDirection.Down) != 0;

            newPivot.y = up && down ? 0.5f :
                         down       ? 1f   :
                                      0f;
        }

        if (next == _lastSize) return;

        target.pivot     = newPivot;
        target.sizeDelta = next;
        _lastSize        = next;
        _lastText        = label.text;
    }
}