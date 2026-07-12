using System;
using DG.Tweening;
using UnityEngine;

public class WorldUITween
{
    internal float DurationVal  { get; private set; } = 1f;
    internal float DelayVal     { get; private set; } = 0f;
    internal float FadeOutVal   { get; private set; } = 0f;
    internal float FadeInVal    { get; private set; } = 0f;
    internal float StartAlpha   { get; private set; } = 1f;
    internal Vector3 MoveOffset { get; private set; } = Vector3.zero;
    internal bool AutoReturn    { get; private set; } = true;
    internal Action OnCompleteCallback { get; private set; }
    internal Ease MoveEase { get; private set; } = Ease.OutCubic;
    internal Ease FadeEase { get; private set; } = Ease.Linear;
    internal Ease ScaleEase { get; private set; } = Ease.OutBack;
    
    internal float Rotation { get; private set; } = 0f;
    internal bool RandomRotation { get; private set; } = false;
    internal float RotationRange { get; private set; } = 10f;

    internal float StartScale { get; private set; } = 1f;
    internal float EndScale { get; private set; } = 1f;

    internal bool RandomOffsetX { get; private set; } = false;
    internal float RandomOffsetXRange { get; private set; } = 20f;

    internal bool Shake { get; private set; } = false;
    internal float ShakeStrength { get; private set; } = 10f;
    internal float ShakeDuration { get; private set; } = 0.15f;

    internal bool Pop { get; private set; } = false;
    internal float PopScale { get; private set; } = 1.3f;
    internal float PopDuration { get; private set; } = 0.15f;

    internal bool FaceCamera { get; private set; } = false;
    

    private readonly WorldUIElement _element;
    internal WorldUITween(WorldUIElement element) { _element = element; }
    
    internal bool ScalePunch { get; private set; } = false;
    
    internal Vector3 StartOffset { get; private set; } = Vector3.zero;

    public WorldUITween From(Vector3 offset)        { StartOffset = offset;              return this; }
    public WorldUITween FromBelow(float amount = 30f) { StartOffset = new Vector3(0, -amount, 0); return this; }
    public WorldUITween FromAbove(float amount = 30f) { StartOffset = new Vector3(0,  amount, 0); return this; }

    public WorldUITween WithPunch()     { ScalePunch = true; return this; }

    public WorldUITween Duration(float v)      { DurationVal = v;           return this; }
    public WorldUITween Delay(float v)         { DelayVal = v;              return this; }
    public WorldUITween FadeOut(float v)       { FadeOutVal = v;            return this; }
    public WorldUITween FadeIn(float v = 0.3f) { FadeInVal = v; StartAlpha = 0f; return this; }
    public WorldUITween MoveUp(float v = 50f, Ease ease = Ease.OutCubic)
    {
        Move(new Vector3(0, v, 0));
        return this;
    }
    public WorldUITween Move(Vector3 v, Ease ease = Ease.OutCubic)
    {
        MoveEase = ease;
        MoveOffset = v;            
        return this;
    }
    public WorldUITween OnComplete(Action cb)  { OnCompleteCallback = cb;   return this; }
    
    public WorldUITween Keep()                 { AutoReturn = false;        return this; }
    public WorldUITween ReturnOnComplete()     { AutoReturn = true;         return this; }

    public WorldUIElement Play()
    {
        _element.Play(this);
        return _element;
    }

    public WorldUITween SetScaleEase(Ease scaleEase)
    {
        ScaleEase = scaleEase;
        return this;
    }
    public WorldUITween SetFadeEase(Ease fadeEase)
    {
        FadeEase = fadeEase;
        return this;
    }
    public WorldUITween Rotate(float angle)
    {
        Rotation = angle;
        return this;
    }

    public WorldUITween RandomRotate(float range = 10f)
    {
        RandomRotation = true;
        RotationRange = range;
        return this;
    }

    public WorldUITween Scale(float start, float end = 1f)
    {
        StartScale = start;
        EndScale = end;
        return this;
    }

    public WorldUITween RandomX(float range = 20f)
    {
        RandomOffsetX = true;
        RandomOffsetXRange = range;
        return this;
    }
    
    public WorldUITween RandomMove(
        float xMin,
        float xMax,
        float yMin,
        float yMax,
        Ease ease = Ease.OutCubic)
    {
        Move(
            new Vector3(
                UnityEngine.Random.Range(xMin, xMax),
                UnityEngine.Random.Range(yMin, yMax),
                0),
            ease);

        return this;
    }
    
    internal float EndRotation { get; private set; }
    internal bool RotateOverTime { get; private set; }

    public WorldUITween Rotate(float start, float end)
    {
        Rotation = start;
        EndRotation = end;
        RotateOverTime = true;
        return this;
    }

    public WorldUITween WithShake(float strength = 10f, float duration = 0.15f)
    {
        Shake = true;
        ShakeStrength = strength;
        ShakeDuration = duration;
        return this;
    }
    
    public WorldUITween PopEffect(float scale = 1.3f, float duration = 0.15f)
    {
        Pop = true;
        PopScale = scale;
        PopDuration = duration;
        return this;
    }

    public WorldUITween Billboard()
    {
        FaceCamera = true;
        return this;
    }
}