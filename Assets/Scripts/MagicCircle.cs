using System;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
[ExecuteAlways]
public class MagicCircle : SerializedMonoBehaviour
{
    [SerializeField]
    private MeshFilter _meshFilter;

    [ListDrawerSettings(Expanded = true)]
    public BaseCircleFigure[] _figures = Array.Empty<BaseCircleFigure>();

    [BoxGroup("Preview")]
    [ReadOnly]
    [SuffixLabel("sec")]
    private float currentTime;

    [BoxGroup("Preview")]
    public float duration = 5f;

    [BoxGroup("Preview")]
    public bool loop;

    [BoxGroup("Preview")]
    [ReadOnly]
    public bool playing;

    private MeshBuilder builder;

#if UNITY_EDITOR
    private double _lastEditorTime;
#endif

    private void OnEnable()
    {
        Rebuild();
        
#if UNITY_EDITOR
        _lastEditorTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += EditorTick;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
#endif
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
#endif

        builder?.Dispose();
        builder = null;
    }

    [Button(ButtonSizes.Medium)]
    public void Draw()
    {
        Rebuild();
    }

    [Button(ButtonSizes.Medium)]
    public void Play()
    {
        playing = true;

#if UNITY_EDITOR
        _lastEditorTime = EditorApplication.timeSinceStartup;
        EditorApplication.QueuePlayerLoopUpdate();
#endif
    }

    [Button(ButtonSizes.Medium)]
    public void Pause()
    {
        playing = false;
    }

    [Button(ButtonSizes.Medium)]
    public void Stop()
    {
        playing = false;
        currentTime = 0f;

        Evaluate();
    }

    [Button(ButtonSizes.Medium)]
    public void Restart()
    {
        currentTime = 0f;
        playing = true;

#if UNITY_EDITOR
        _lastEditorTime = EditorApplication.timeSinceStartup;
        EditorApplication.QueuePlayerLoopUpdate();
#endif

        Evaluate();
    }

    private void Rebuild()
    {
        builder?.Dispose();

        builder = new MeshBuilder(_meshFilter);

        for (int i = 0; i < _figures.Length; i++)
            _figures[i].FinalBuild(builder);

        builder.BuildMesh();

        currentTime = 0f;
        Evaluate();
    }

    private unsafe void Evaluate()
    {
        if (builder == null)
            return;

        for (int i = 0; i < _figures.Length; i++)
            EvaluateFigure(_figures[i]);

        builder.UpdateMesh();
    }

    private unsafe void EvaluateFigure(BaseCircleFigure figure)
    {
        TweenContext context = new TweenContext
        {
            builder = builder,
            transform = figure.transform.TransformData
        };

        for (int i = 0; i < figure.Tweens.Length; i++)
            figure.Tweens[i].Evaluate(context, currentTime);

        for (int i = 0; i < figure.childs.Length; i++)
            EvaluateFigure(figure.childs[i]);
    }

#if UNITY_EDITOR
    private void EditorTick()
    {
        if (!this || !playing)
            return;

        double now = EditorApplication.timeSinceStartup;
        double delta = now - _lastEditorTime;

        _lastEditorTime = now;

        currentTime += (float)delta;

        if (currentTime >= duration)
        {
            if (loop)
            {
                currentTime %= duration;
            }
            else
            {
                currentTime = duration;
                playing = false;
            }
        }

        Evaluate();

        EditorApplication.QueuePlayerLoopUpdate();
    }
#endif
}

public unsafe struct TweenContext
{
    public TransformData* transform;
    public FigureRuntime* runtime;
    public MeshBuilder builder;
}


public abstract class BaseTween
{
    public float duration = 1f;
    public float delay;

    public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public abstract void Evaluate(TweenContext context, float currentTime);
}

[Serializable]
public unsafe class MoveToTween : BaseTween
{
    public Vector3 from;
    public Vector3 target;

    public override void Evaluate(TweenContext context, float currentTime)
    {
        float time = currentTime - delay;

        if (time <= 0f)
        {
            context.transform->localPosition = from;
            return;
        }

        float t = Mathf.Clamp01(time / duration);
        t = curve.Evaluate(t);

        context.transform->localPosition = Vector3.LerpUnclamped(from, target, t);
    }
}
[Serializable]
public unsafe class ColorTween : BaseTween
{
    public Color from = Color.white;
    public Color target = Color.white;
 
    public override void Evaluate(TweenContext context, float currentTime)
    {
        if (context.runtime == null) return;
 
        float time = currentTime - delay;
        if (time <= 0f)
        {
            context.runtime->color = from;
            return;
        }
 
        float t = curve.Evaluate(Mathf.Clamp01(time / duration));
        context.runtime->color = Color.LerpUnclamped(from, target, t);
    }
}

[Serializable]
public class SpriteFigure : BaseCircleFigure
{
    public Sprite sprite;
 
    protected override void Build(MeshBuilder builder)
    {
        if (sprite != null)
            builder.BuildSprite(sprite);
    }
}


[Serializable]
public unsafe class RotateToTween : BaseTween
{
    public Vector3 from;
    public Vector3 target;

    public override void Evaluate(TweenContext context, float currentTime)
    {
        float time = currentTime - delay;

        if (time <= 0f)
        {
            context.transform->localRotation = Quaternion.Euler(from);
            return;
        }

        float t = Mathf.Clamp01(time / duration);
        t = curve.Evaluate(t);

        context.transform->localRotation = Quaternion.Euler(Vector3.LerpUnclamped(from, target, t));
    }
}

[Serializable]
public unsafe class ScaleToTween : BaseTween
{
    public Vector3 from = Vector3.one;
    public Vector3 target = Vector3.one;

    public override void Evaluate(TweenContext context, float currentTime)
    {
        float time = currentTime - delay;

        if (time <= 0f)
        {
            context.transform->localScale = from;
            return;
        }

        float t = Mathf.Clamp01(time / duration);
        t = curve.Evaluate(t);

        context.transform->localScale = Vector3.LerpUnclamped(from, target, t);
    }
}

[Serializable]
public abstract class BaseCircleFigure
{
    public virtual unsafe void FinalBuild(MeshBuilder builder, BaseCircleFigure parent = null)
    {
        Build(builder);

        builder.SetTransformDataPtr(ref transform.TransformData);
        builder.SetRuntimeDataPtr(ref runtime.Runtime);

        transform.MeshBuilder = builder;
        runtime.MeshBuilder = builder;

        if (parent != null)
            transform.TransformData->parent = parent.transform.TransformData;

        transform.OnDataChange();
        runtime.OnDataChange();

        for (int i = 0; i < childs.Length; i++)
        {
            childs[i].FinalBuild(builder, this);
        }
    }
    protected abstract void Build(MeshBuilder builder);

    public SerializedTransform transform = new SerializedTransform();
    public SerializedRuntime runtime = new SerializedRuntime();
    public BaseCircleFigure[] childs = Array.Empty<BaseCircleFigure>();
    public BaseTween[] Tweens = Array.Empty<BaseTween>();
}

[Serializable]
public unsafe class SerializedTransform
{
    [NonSerialized] public TransformData* TransformData;
    [NonSerialized] public MeshBuilder MeshBuilder;

    [OnValueChanged(nameof(OnDataChange))]
    public Vector3 position;

    [OnValueChanged(nameof(OnDataChange))]
    public Vector3 rotation;

    [OnValueChanged(nameof(OnDataChange))]
    public Vector3 scale = Vector3.one;

    public void OnDataChange()
    {
        if (TransformData == null)
            return;

        TransformData->localPosition = position;
        TransformData->rotation = Quaternion.Euler(rotation);
        TransformData->localScale = scale;
        
        MeshBuilder.UpdateMesh();
    }
}
[Serializable]
public unsafe class SerializedRuntime
{
    [NonSerialized] public FigureRuntime* Runtime;
    [NonSerialized] public MeshBuilder MeshBuilder;

    [OnValueChanged(nameof(OnDataChange))]
    [ShowIf(nameof(ColorShowCondition))]
    public Color color = Color.white;

    public bool ColorShowCondition()
    {
        return Runtime != null;
    }

    public void OnDataChange()
    {
        if (Runtime == null)
            return;
        Runtime->color = color;
        
        MeshBuilder.UpdateMesh();
    }
}

[Serializable]
public class RootMesh : BaseCircleFigure
{
    protected override void Build(MeshBuilder builder)
    {
    }

    public override unsafe void FinalBuild(MeshBuilder builder, BaseCircleFigure parent = null)
    {
        transform.TransformData = builder.Transform;
        transform.MeshBuilder = builder;
        transform.OnDataChange();

        runtime.MeshBuilder = builder;
        runtime.Runtime = null;
        runtime.OnDataChange();

        for (int i = 0; i < childs.Length; i++)
        {
            childs[i].FinalBuild(builder, this);
        }
    }
}


[Serializable]
public class TriangleFigure : BaseCircleFigure
{
    protected override void Build(MeshBuilder builder)
    {
        builder.BuildTriangle();
    }
}
[Serializable]
public class CircleFigure : BaseCircleFigure
{
    [Min(0f)]
    public float radius = 1f;

    [Min(3)]
    public int segments = 64;

    protected override void Build(MeshBuilder builder)
    {
        builder.BuildCircle(radius, segments);
    }
}

[Serializable]
public class RingFigure : BaseCircleFigure
{
    [Min(0f)]
    public float innerRadius = 0.5f;

    [Min(0f)]
    public float outerRadius = 1f;

    [Min(3)]
    public int segments = 64;

    protected override void Build(MeshBuilder builder)
    {
        builder.BuildRing(innerRadius, outerRadius, segments);
    }
}

[Serializable]
public class ArcFigure : BaseCircleFigure
{
    [Min(0f)]
    public float radius = 1f;

    public float startAngle;
    public float endAngle = 90f;

    [Min(1)]
    public int segments = 16;

    public bool filled = true;

    protected override void Build(MeshBuilder builder)
    {
        builder.BuildArc(
            radius,
            startAngle,
            endAngle,
            segments,
            filled);
    }
}

[Serializable]
public class RingArcFigure : BaseCircleFigure
{
    [Min(0f)]
    public float innerRadius = 0.5f;

    [Min(0f)]
    public float outerRadius = 1f;

    public float startAngle;
    public float endAngle = 90f;

    [Min(1)]
    public int segments = 16;

    protected override void Build(MeshBuilder builder)
    {
        builder.BuildRingArc(innerRadius, outerRadius, startAngle, endAngle, segments);
    }
}

[Serializable]
public class RegularPolygonFigure : BaseCircleFigure
{
    [Min(0f)]
    public float radius = 1f;

    [Min(3)]
    public int sides = 6;

    public float rotation;

    protected override void Build(MeshBuilder builder)
    {
        builder.BuildRegularPolygon(radius, sides, rotation);
    }
}
[Serializable]
public class StarFigure : BaseCircleFigure
{
    [Min(0f)]
    public float outerRadius = 1f;

    [Min(0f)]
    public float innerRadius = 0.5f;

    [Min(2)]
    public int points = 5;

    public float rotation = -90f;

    protected override void Build(MeshBuilder builder)
    {
        builder.BuildStar(outerRadius, innerRadius, points, rotation);
    }
}