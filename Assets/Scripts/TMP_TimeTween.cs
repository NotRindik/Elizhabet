using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

public class TMP_TimeTween : SerializedMonoBehaviour
{
    public TMP_Text text;
    
    public TMP_TimeTweenBase[] tweens = new TMP_TimeTweenBase[0];

    private void OnValidate()
    {
        text ??= GetComponent<TMP_Text>();
    }

    public void OnUpdate(float tick)
    {
        if(text == null)
            return;
        
        foreach (var tween in tweens)
        {
            tween.Evaluate(text,tick);
        }
    }
    
    
    public abstract class TMP_TimeTweenBase
    {
        public abstract void Evaluate(TMP_Text text, float time);
    }

    public class ColorTween : TMP_TimeTweenBase
    {
        public Color from,to;
        public AnimationCurve curve = new AnimationCurve();
        public override void Evaluate(TMP_Text text, float time)
        {
            text.color = Color.LerpUnclamped(from, to, curve.Evaluate(time));
        }
    }

}