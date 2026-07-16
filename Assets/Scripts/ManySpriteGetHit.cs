using DG.Tweening;
using Systems;
using UnityEngine;

public sealed class ManySpriteGetHit : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] renderers;
    [SerializeField] private float flashDuration = 0.2f;

    private static readonly int FlashID = Shader.PropertyToID("_FlashAmount");

    private MaterialPropertyBlock _mpb;
    private Tween _tween;
    private float _value;

    private AbstractEntity _entity;

    private HealthComponent _health;
    public bool autoSetup = true;

    private void Start()
    {
        _mpb = new MaterialPropertyBlock();
        _entity = GetComponent<AbstractEntity>();

        if (autoSetup && _entity)
        {
            _health = _entity.GetControllerComponent<HealthComponent>();
            if(_health != null)
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
        ).SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (!r) continue;
                }
            });
    }

    private void Apply(float value)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (!r) continue;
            
            _mpb.SetFloat(FlashID, value);
            r.SetPropertyBlock(_mpb);
        }
    }

    private void OnDestroy()
    {
        if(autoSetup && _entity && _health != null)
            _health.OnTakeHit -= HitSub;
        _tween?.Kill();
    }
}
