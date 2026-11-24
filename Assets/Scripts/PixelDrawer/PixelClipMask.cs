using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways, RequireComponent(typeof(SpriteMask))]
public class PixelClipMask : MonoBehaviour
{
    static readonly List<PixelClipMask> active = new List<PixelClipMask>();

    SpriteMask mask;
    SpriteRenderer source;

    public SpriteMask Mask
    {
        get
        {
            if(mask == null) mask = GetComponent<SpriteMask>();
            return mask;
        }
    }

    public SpriteRenderer Source
    {
        get
        {
            if(source == null && transform.parent != null) source = transform.parent.GetComponent<SpriteRenderer>();
            return source;
        }
    }

    void OnEnable()
    {
        if(!active.Contains(this)) active.Add(this);
        Refresh();
    }

    void OnDisable()
    {
        active.Remove(this);
    }

    void LateUpdate()
    {
        Refresh();
    }

    public void Refresh()
    {
        var m = Mask;
        if(m == null) return;
        var s = Source;
        var sprite = s != null ? s.sprite : null;
        if(m.sprite != sprite) m.sprite = sprite;
        var on = s != null && s.enabled && sprite != null;
        if(m.enabled != on) m.enabled = on;
    }

    public static void RefreshAll()
    {
        for(var i = active.Count - 1; i >= 0; i--)
        {
            if(active[i] == null) active.RemoveAt(i);
            else active[i].Refresh();
        }
    }
}
