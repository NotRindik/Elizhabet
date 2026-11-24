#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

public class PixelScrollArea : VisualElement
{
    const float WheelStep = 18f;
    const float BarSize = 13f;

    readonly VisualElement viewport;
    readonly VisualElement content;
    readonly Scroller vertical;
    readonly Scroller horizontal;
    readonly bool allowVertical;
    readonly bool allowHorizontal;
    readonly bool showBars;
    Vector2 offset;

    public event System.Action<Vector2> OffsetChanged;

    public override VisualElement contentContainer
    {
        get { return content; }
    }

    public Vector2 Offset
    {
        get { return offset; }
        set { SetOffset(value, true); }
    }

    public PixelScrollArea(bool vertical, bool horizontal, bool showBars)
    {
        allowVertical = vertical;
        allowHorizontal = horizontal;
        this.showBars = showBars;
        style.flexGrow = 1;
        style.flexShrink = 1;
        style.flexDirection = FlexDirection.Column;
        style.overflow = Overflow.Hidden;

        var row = new VisualElement();
        row.style.flexGrow = 1;
        row.style.flexDirection = FlexDirection.Row;
        hierarchy.Add(row);

        viewport = new VisualElement();
        viewport.style.flexGrow = 1;
        viewport.style.flexShrink = 1;
        viewport.style.overflow = Overflow.Hidden;
        row.Add(viewport);

        content = new VisualElement();
        content.style.position = Position.Absolute;
        content.style.left = 0;
        content.style.top = 0;
        if(!horizontal) content.style.right = 0;
        viewport.Add(content);

        this.vertical = new Scroller(0, 0, v => SetOffset(new Vector2(offset.x, v), false), SliderDirection.Vertical);
        this.vertical.style.width = BarSize;
        this.vertical.style.display = DisplayStyle.None;
        row.Add(this.vertical);

        this.horizontal = new Scroller(0, 0, v => SetOffset(new Vector2(v, offset.y), false), SliderDirection.Horizontal);
        this.horizontal.style.height = BarSize;
        this.horizontal.style.display = DisplayStyle.None;
        hierarchy.Add(this.horizontal);

        viewport.RegisterCallback<GeometryChangedEvent>(e => UpdateBars());
        content.RegisterCallback<GeometryChangedEvent>(e => UpdateBars());
        RegisterCallback<WheelEvent>(OnWheel);
    }

    public void ScrollBy(Vector2 delta)
    {
        SetOffset(offset + delta, true);
    }

    void OnWheel(WheelEvent e)
    {
        var d = e.delta * WheelStep;
        if(allowHorizontal && (!allowVertical || e.shiftKey)) d = new Vector2(d.y != 0 ? d.y : d.x, 0);
        var before = offset;
        ScrollBy(new Vector2(allowHorizontal ? d.x : 0, allowVertical ? d.y : 0));
        if(before != offset) e.StopPropagation();
    }

    Vector2 MaxOffset()
    {
        var view = viewport.layout;
        var size = content.layout;
        var maxX = allowHorizontal && !float.IsNaN(size.width) ? Mathf.Max(0, size.width - view.width) : 0;
        var maxY = allowVertical && !float.IsNaN(size.height) ? Mathf.Max(0, size.height - view.height) : 0;
        return new Vector2(float.IsNaN(maxX) ? 0 : maxX, float.IsNaN(maxY) ? 0 : maxY);
    }

    void SetOffset(Vector2 value, bool updateBars)
    {
        var max = MaxOffset();
        value = new Vector2(Mathf.Clamp(value.x, 0, max.x), Mathf.Clamp(value.y, 0, max.y));
        if(value == offset) return;
        offset = value;
        content.style.left = -offset.x;
        content.style.top = -offset.y;
        if(updateBars)
        {
            vertical.slider.SetValueWithoutNotify(offset.y);
            horizontal.slider.SetValueWithoutNotify(offset.x);
        }
        if(OffsetChanged != null) OffsetChanged(offset);
    }

    void UpdateBars()
    {
        var max = MaxOffset();
        var view = viewport.layout;
        var size = content.layout;
        var showV = showBars && allowVertical && max.y > 0.5f;
        var showH = showBars && allowHorizontal && max.x > 0.5f;
        vertical.style.display = showV ? DisplayStyle.Flex : DisplayStyle.None;
        horizontal.style.display = showH ? DisplayStyle.Flex : DisplayStyle.None;
        vertical.highValue = max.y;
        horizontal.highValue = max.x;
        if(size.height > 0) vertical.Adjust(Mathf.Clamp01(view.height / size.height));
        if(size.width > 0) horizontal.Adjust(Mathf.Clamp01(view.width / size.width));
        SetOffset(offset, true);
        vertical.slider.SetValueWithoutNotify(offset.y);
        horizontal.slider.SetValueWithoutNotify(offset.x);
    }
}
#endif
