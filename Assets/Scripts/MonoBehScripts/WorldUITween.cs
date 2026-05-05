using System;
using UnityEngine;

public class WorldUITween
{
    internal float DurationVal  { get; private set; } = 1f;
    internal float DelayVal     { get; private set; } = 0f;
    internal float FadeOutVal   { get; private set; } = 0f;
    internal float FadeInVal    { get; private set; } = 0f;
    internal float StartAlpha   { get; private set; } = 1f;
    internal Vector3 MoveOffset { get; private set; } = Vector3.zero;
    internal bool AutoReturn    { get; private set; } = true;  // по умолчанию возвращает
    internal Action OnCompleteCallback { get; private set; }

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
    public WorldUITween MoveUp(float v = 50f)  { MoveOffset = new Vector3(0, v, 0); return this; }
    public WorldUITween Move(Vector3 v)        { MoveOffset = v;            return this; }
    public WorldUITween OnComplete(Action cb)  { OnCompleteCallback = cb;   return this; }

    // Декларативное управление возвратом в пул
    public WorldUITween Keep()                 { AutoReturn = false;        return this; }
    public WorldUITween ReturnOnComplete()     { AutoReturn = true;         return this; }

    public WorldUIElement Play()
    {
        _element.Play(this);
        return _element;
    }
}