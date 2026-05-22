using DG.Tweening;
using UnityEngine;

public class SecretReveal : MonoBehaviour
{
    [SerializeField]
    private SpriteRenderer overlay;

    [SerializeField]
    private float fadeSpeed = 5f;

    private Tween _tween;
    

    public void Reveal()
    {
        _tween?.Kill();
        _tween = overlay.DOFade(0, fadeSpeed);
    }

    public void Hide()
    {
        _tween?.Kill();
        _tween = overlay.DOFade(1, fadeSpeed);
    }
}