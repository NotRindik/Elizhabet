using System.Collections.Generic;
using UnityEngine;

[PreferBinarySerialization]
public class PixelArtAsset : ScriptableObject
{
    public enum LoopMode { Loop, Once, PingPong }

    [System.Serializable]
    public class Frame
    {
        public Texture2D color;
        public Texture2D emission;
        public Sprite sprite;
        public Vector2Int origin;
        [Min(0.01f)] public float duration = 0.1f;
    }

    public List<Frame> frames = new List<Frame>();
    public float pixelsPerUnit = 16;
    public LoopMode loop;
    public Material material;

    public int Count
    {
        get { return frames.Count; }
    }

    public bool HasEmission
    {
        get
        {
            for(var i = 0; i < frames.Count; i++)
                if(frames[i].emission != null) return true;
            return false;
        }
    }

    public float TotalDuration
    {
        get
        {
            var total = 0f;
            for(var i = 0; i < frames.Count; i++) total += Mathf.Max(0.001f, frames[i].duration);
            return total;
        }
    }

    public int FrameAt(float time)
    {
        var n = frames.Count;
        if(n <= 1) return 0;
        var total = TotalDuration;
        float t;
        if(loop == LoopMode.Once) t = Mathf.Min(time, total - 0.0001f);
        else if(loop == LoopMode.Loop) t = Mathf.Repeat(time, total);
        else
        {
            var m = Mathf.Repeat(time, total * 2);
            t = Mathf.Min(m < total ? m : total * 2 - m, total - 0.0001f);
        }
        if(t < 0) t = 0;
        var acc = 0f;
        for(var i = 0; i < n; i++)
        {
            acc += Mathf.Max(0.001f, frames[i].duration);
            if(t < acc) return i;
        }
        return n - 1;
    }
}
