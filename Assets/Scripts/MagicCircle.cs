using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
[ExecuteAlways]
public class MagicCircle : SerializedMonoBehaviour
{
    [SerializeField]
    private MeshFilter _meshFilter;
    [SerializeField] private Texture edgeTexture;

    [ListDrawerSettings(Expanded = true)]
    public BaseCircleFigure[] _figures = new BaseCircleFigure[0];

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

    
    [BoxGroup("Preview")]
    public CircleParticle[] particles = new CircleParticle[0];
    
    private MeshBuilder builder;
    
    public UnityEvent OnPlay,OnEnd;
    public UnityEvent<float> OnTick,OnTickUnnormalized;

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
        builder?.Dispose();
        builder = null;
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
    
    public void Play()
    {
        playing = true;
        OnPlay.Invoke();

#if UNITY_EDITOR
        _lastEditorTime = EditorApplication.timeSinceStartup;
        EditorApplication.QueuePlayerLoopUpdate();
#endif
    }

    [Button("Play/Pause",ButtonSizes.Medium)]
    private void PlayPause()
    {
        if (playing)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }
    
    private void Update()
    {
        if (!playing || !Application.isPlaying) return;
        Tick(Time.deltaTime);
    }

    private void Tick(float delta)
    {
        currentTime += delta;

        if (currentTime >= duration)
        {
            if (loop)
            {
                OnPlay.Invoke();
                currentTime %= duration;
            }
            else
            {
                currentTime = duration;
                playing = false;
                OnEnd.Invoke();
            }
        }

        Evaluate();
        SimulateParticles();
        OnTick.Invoke(currentTime / duration);
        OnTickUnnormalized.Invoke(currentTime);
    }
    
    private void SimulateParticles()
    {
        for (int i = 0; i < particles.Length; i++)
        {
            var p = particles[i];
            if (p.particleSystem == null) continue;

            float local = currentTime - p.startTime;
            if (local < 0f)
            {
                p.particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                continue;
            }

            p.particleSystem.Simulate(local, true, true, true);
        }
    }
    
    public void Pause()
    {
        playing = false;
    }

    [Button(ButtonSizes.Medium)]
    public void Stop()
    {
        playing = false;
        currentTime = 0f;
        OnTick.Invoke(0);
        OnTickUnnormalized.Invoke(currentTime);

        Evaluate();
    }

    [Button(ButtonSizes.Medium)]
    public void Restart()
    {
        OnPlay.Invoke();
        currentTime = 0f;
        playing = true;
        OnTick.Invoke(0);
        OnTickUnnormalized.Invoke(currentTime);

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
        builder.EdgeTexture = edgeTexture;
        
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
            runtime = figure.runtime.Runtime,
            transform = figure.transform.TransformData
        };

        for (int i = 0; i < figure.Tweens.Length; i++)
            figure.Tweens[i].Evaluate(context, currentTime);
        
        UpdateLinkedTransforms(figure);
        
        for (int i = 0; i < figure.childs.Length; i++)
        {
            EvaluateFigure(figure.childs[i]);
        }
    }
    
    private unsafe void UpdateLinkedTransforms(BaseCircleFigure figure)
    {
        TransformData* parent = figure.transform.TransformData;
        if (figure.transform.linkedTransforms == null)
            figure.transform.linkedTransforms = new List<LinkedTransform>();

        for (int i = 0; i < figure.transform.linkedTransforms.Count; i++)
        {
            var linked = figure.transform.linkedTransforms[i];
            if (linked == null || linked.transform == null)
                continue;

            Vector3 localPos = parent->position + parent->rotation * Vector3.Scale(parent->lossyScale, linked.localPosition);

            linked.transform.position = transform.TransformPoint(localPos);
            linked.transform.rotation = transform.rotation * parent->rotation * Quaternion.Euler(linked.localRotation);
            linked.transform.localScale = Vector3.Scale(transform.lossyScale, Vector3.Scale(parent->lossyScale, linked.localScale));
        }
    }

#if UNITY_EDITOR
    private void EditorTick()
    {
        if (!this || !playing || Application.isPlaying) return;

        double now = EditorApplication.timeSinceStartup;
        double delta = now - _lastEditorTime;
        _lastEditorTime = now;

        Tick((float)delta);
        EditorApplication.QueuePlayerLoopUpdate();
    }
#endif
    
    
    [Serializable]
    public class CircleParticle
    {
        public ParticleSystem particleSystem;
        public float startTime;
    }
}

public unsafe struct TweenContext
{
    public TransformData* transform;
    public FigureRuntime* runtime;
    public MeshBuilder builder;
}
[Serializable]
public class LinkedTransform
{
    public Transform transform;

    public Vector3 localPosition;
    public Vector3 localRotation;
    public Vector3 localScale = Vector3.one;
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
            return;
        }

        float t = Mathf.Clamp01(time / duration);
        t = curve.Evaluate(t);

        context.transform->localPosition = Vector3.LerpUnclamped(from, target, t);
    }
}
public abstract unsafe class FloatFieldTween : BaseTween
{
    public float from;
    public float target;

    protected abstract float* GetFieldPtr(FigureRuntime* runtime);

    public override void Evaluate(TweenContext context, float currentTime)
    {
        if (context.runtime == null) return;
        float* field = GetFieldPtr(context.runtime);

        float time = currentTime - delay;
        if (time <= 0f)
        {
            return;
        }

        float t = curve.Evaluate(Mathf.Clamp01(time / duration));
        *field = Mathf.LerpUnclamped(from, target, t);
    }
}

public abstract unsafe class Vector2FieldTween : BaseTween
{
    public Vector2 from;
    public Vector2 target;

    protected abstract Vector2* GetFieldPtr(FigureRuntime* runtime);

    public override void Evaluate(TweenContext context, float currentTime)
    {
        if (context.runtime == null) return;
        Vector2* field = GetFieldPtr(context.runtime);

        float time = currentTime - delay;
        if (time <= 0f)
        {
            return;
        }

        float t = curve.Evaluate(Mathf.Clamp01(time / duration));
        *field = Vector2.LerpUnclamped(from, target, t);
    }
}

public abstract unsafe class ColorFieldTween : BaseTween
{
    public Color from = Color.white;
    public Color target = Color.white;

    protected abstract Color* GetFieldPtr(FigureRuntime* runtime);

    public override void Evaluate(TweenContext context, float currentTime)
    {
        if (context.runtime == null) return;
        Color* field = GetFieldPtr(context.runtime);

        float time = currentTime - delay;
        if (time <= 0f)
        {
            return;
        }

        float t = curve.Evaluate(Mathf.Clamp01(time / duration));
        *field = Color.LerpUnclamped(from, target, t);
    }
}
[Serializable]
public unsafe class EmissionIntensityTween : FloatFieldTween
{
    protected override float* GetFieldPtr(FigureRuntime* r) => &r->emissionIntensity;
}

[Serializable]
public unsafe class RevealProgressTween : FloatFieldTween
{
    protected override float* GetFieldPtr(FigureRuntime* r) => &r->revealProgress;
}

[Serializable]
public unsafe class RevealSoftnessTween : FloatFieldTween
{
    protected override float* GetFieldPtr(FigureRuntime* r) => &r->revealSoftness;
}

[Serializable]
public unsafe class RevealMinYTween : FloatFieldTween
{
    protected override float* GetFieldPtr(FigureRuntime* r) => &r->revealMinY;
}

[Serializable]
public unsafe class RevealMaxYTween : FloatFieldTween
{
    protected override float* GetFieldPtr(FigureRuntime* r) => &r->revealMaxY;
}

[Serializable]
public unsafe class RevealMaxRadiusTween : FloatFieldTween
{
    protected override float* GetFieldPtr(FigureRuntime* r) => &r->revealMaxRadius;
}

[Serializable]
public unsafe class EdgeWidthTween : FloatFieldTween
{
    protected override float* GetFieldPtr(FigureRuntime* r) => &r->edgeWidth;
}

[Serializable]
public unsafe class EdgeIntensityTween : FloatFieldTween
{
    protected override float* GetFieldPtr(FigureRuntime* r) => &r->edgeIntensity;
}

[Serializable]
public unsafe class EdgeDistortionTween : FloatFieldTween
{
    protected override float* GetFieldPtr(FigureRuntime* r) => &r->edgeDistortion;
}

[Serializable]
public unsafe class EdgeNoiseScaleTween : FloatFieldTween
{
    protected override float* GetFieldPtr(FigureRuntime* r) => &r->edgeNoiseScale;
}

[Serializable]
public unsafe class EdgeColorTween : ColorFieldTween
{
    protected override Color* GetFieldPtr(FigureRuntime* r) => &r->edgeColor;
}

[Serializable]
public unsafe class ColorTween : ColorFieldTween
{
    protected override Color* GetFieldPtr(FigureRuntime* r) => &r->color;
}
[Serializable]
public unsafe class EdgeScrollSpeedTween : Vector2FieldTween
{
    protected override Vector2* GetFieldPtr(FigureRuntime* r) => &r->edgeScrollSpeed;
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
    public virtual unsafe void FinalBuild(MeshBuilder builder, BaseCircleFigure parent = null, HashSet<BaseCircleFigure> visited = null)
    {
        visited ??= new HashSet<BaseCircleFigure>();
        if (!visited.Add(this))
        {
            Debug.LogError($"Cycle in figure hierarchy at {GetType().Name}, skipping FinalBuild");
            return;
        }

        Build(builder);
        builder.SetTransformDataPtr(ref transform.TransformData);
        builder.SetRuntimeDataPtr(ref runtime.Runtime);
        transform.MeshBuilder = builder;
        runtime.MeshBuilder = builder;
        if (parent != null) transform.TransformData->parent = parent.transform.TransformData;
        transform.OnDataChange();
        runtime.OnDataChange();

        for (int i = 0; i < childs.Length; i++)
            childs[i].FinalBuild(builder, this, visited);
    }
    protected abstract void Build(MeshBuilder builder);

    public SerializedTransform transform = new SerializedTransform();
    public SerializedRuntime runtime = new SerializedRuntime();
    public BaseCircleFigure[] childs = new BaseCircleFigure[0];
    public BaseTween[] Tweens = new BaseTween[0];
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
    
    public List<LinkedTransform> linkedTransforms = new List<LinkedTransform>();

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

    [OnValueChanged(nameof(OnDataChange))] [ShowIf(nameof(HasRuntime))]
    public Color color = Color.white;

    [BoxGroup("Reveal")] [OnValueChanged(nameof(OnDataChange))] [ShowIf(nameof(HasRuntime))] [MinValue(0f)]
    public float emissionIntensity = 1f;

    [BoxGroup("Reveal")] [OnValueChanged(nameof(OnDataChange))] [ShowIf(nameof(HasRuntime))] [Range(0f, 1f)]
    public float revealProgress = 1f;

    [BoxGroup("Reveal")] [OnValueChanged(nameof(OnDataChange))] [ShowIf(nameof(HasRuntime))]
    public RevealMode revealMode = RevealMode.Up;

    [BoxGroup("Reveal")] [OnValueChanged(nameof(OnDataChange))] [ShowIf(nameof(HasRuntime))] [Range(0.001f, 1f)]
    public float revealSoftness = 0.05f;

    [BoxGroup("Reveal")] [OnValueChanged(nameof(OnDataChange))] [ShowIf(nameof(HasRuntime))] [MinValue(0.0001f)]
    public float revealMaxRadius = 1f;

    [BoxGroup("Edge Fire")] [OnValueChanged(nameof(OnDataChange))] [ShowIf(nameof(HasRuntime))] [MinValue(0f)]
    public float edgeWidth = 0.1f;

    [BoxGroup("Edge Fire")] [OnValueChanged(nameof(OnDataChange))] [ShowIf(nameof(HasRuntime))] [MinValue(0f)]
    public float edgeIntensity = 1f;

    [BoxGroup("Edge Fire")] [OnValueChanged(nameof(OnDataChange))] [ShowIf(nameof(HasRuntime))]
    public Color edgeColor = Color.white;

    [BoxGroup("Edge Fire")] [OnValueChanged(nameof(OnDataChange))] [ShowIf(nameof(HasRuntime))]
    public Vector2 edgeScrollSpeed = new Vector2(0f, 0.5f);

    [BoxGroup("Edge Fire")] [OnValueChanged(nameof(OnDataChange))] [ShowIf(nameof(HasRuntime))] [MinValue(0f)]
    public float edgeDistortion = 0.1f;

    [BoxGroup("Edge Fire")] [OnValueChanged(nameof(OnDataChange))] [ShowIf(nameof(HasRuntime))] [MinValue(0.0001f)]
    public float edgeNoiseScale = 1f;

    public bool HasRuntime() => Runtime != null;

    public void OnDataChange()
    {
        if (Runtime == null) return;
        Runtime->color = color;
        Runtime->emissionIntensity = emissionIntensity;
        Runtime->revealProgress = revealProgress;
        Runtime->revealMode = (float)revealMode;
        Runtime->revealSoftness = revealSoftness;
        Runtime->revealMaxRadius = revealMaxRadius;
        Runtime->edgeWidth = edgeWidth;
        Runtime->edgeIntensity = edgeIntensity;
        Runtime->edgeColor = edgeColor;
        Runtime->edgeScrollSpeed = edgeScrollSpeed;
        Runtime->edgeDistortion = edgeDistortion;
        Runtime->edgeNoiseScale = edgeNoiseScale;
        MeshBuilder.UpdateMesh();
    }
}
[Serializable]
public class RootMesh : BaseCircleFigure
{
    protected override void Build(MeshBuilder builder)
    {
    }

    public override unsafe void FinalBuild(MeshBuilder builder, BaseCircleFigure parent = null, HashSet<BaseCircleFigure> visited = null)
    {
        visited ??= new HashSet<BaseCircleFigure>();
        if (!visited.Add(this))
        {
            Debug.LogError($"Cycle in figure hierarchy at {GetType().Name}, skipping FinalBuild");
            return;
        }
        
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