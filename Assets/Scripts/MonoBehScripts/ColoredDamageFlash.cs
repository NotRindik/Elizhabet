using System;
using DG.Tweening;
using std.UnityUtilities;
using Systems;
using UnityEngine;

public class ColoredDamageFlash : MonoBehaviour
{
    [SerializeField] private RenderersContainer _renders;
    [SerializeField] private float flashDuration = 0.2f;
    [SerializeField] private Color flashColor;
    
    private Tween _tween;
    private float _value;

    private AbstractEntity _entity;

    private HealthComponent _health;
    public bool autoSetup = true;

    private void Start()
    {
        _entity = GetComponent<AbstractEntity>();
        
        if (autoSetup && _entity)
        {
            _health = _entity.GetControllerComponent<HealthComponent>();
            
            _health.OnTakeHit += HitSub;
        }
    }

    private void HitSub(HitInfo _) => GetHit();
    
    public void GetHit()
    {
        
        _tween?.Kill();

        _value = 1f;
        Apply(_value);

        _tween = DOTween.To(
            () => _value,
            v =>
            {
                _value = v;
                Apply(v);
            },
            0f,
            flashDuration
        ).SetEase(Ease.OutQuad);
    }

    private void Apply(float value)
    {
        for (int i = 0; i < _renders.renderers.Length; i++)
        {
            var r = _renders.renderers[i];
            
            if (!r) 
                continue;

            Vector3 baseColor = Color.white.ParseToVector3();
            Vector3 redColor = Color.red.ParseToVector3();

            r.color = Vector3.Lerp(baseColor, redColor, value).ParseToVector3();
        }
    }

    private void OnDestroy()
    {
        if(autoSetup && _entity && _health != null)
            _health.OnTakeHit -= HitSub;
        _tween?.Kill();
    }
}
