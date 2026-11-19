using System;
using UnityEngine;
public class WorldMessage : MonoBehaviour
{
    [SerializeField] private float fadeIn  = 0.3f;
    [SerializeField] private float fadeOut = 0.3f;
    [SerializeField] private Transform target = null;

    [TextArea]
    public string text;

    private WorldUIElement _tip;

    public void Show()
    {
        if (!target) target = transform;
        
        if (_tip) _tip.Kill();
        _tip = WorldUIManager.Instance
            .Spawn("dialogue", target,text) 
            .FadeIn(fadeIn)
            .Delay(0.2f)
            .MoveUp(-40)
            .FromAbove(200)
            .Keep()
            .Play()
            .SetScale(1);
    }

    public void Hide()
    {
        if (_tip) _tip.Hide(fadeOut, onComplete: () => _tip = null);
    }
    
    public void Toggle()
    {
        if (_tip != null) Hide();
        else Show();
    }
}