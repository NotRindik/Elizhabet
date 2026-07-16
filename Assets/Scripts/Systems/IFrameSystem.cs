using System;
using System.Collections;
using Systems;
using UnityEngine;

public class IFrameSystem : BaseSystem,IDisposable
{
    private HealthComponent _healthC;
    private IFrameComponent IframeC;
    private HealthSystem _healthS;
    private RenderersContainer renderers;

    public Coroutine InvincibleProcess;
    public Coroutine BlinkProcess;

    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        _healthC = owner.GetControllerComponent<HealthComponent>();
        IframeC = owner.GetControllerComponent<IFrameComponent>();
        _healthS = owner.GetControllerSystem<HealthSystem>();

        renderers = owner.GetComponent<RenderersContainer>();
        
        _healthC.OnTakeHit += OnTakeHit;
    }

    public void OnTakeHit(HitInfo hitInfo)
    {
        if(InvincibleProcess != null)
            owner.StopCoroutine(InvincibleProcess);
        
        InvincibleProcess = owner.StartCoroutine(IFrameProcess());
    }

    public IEnumerator IFrameProcess()
    {
        _healthS.IsActive = false;
        
        BlinkProcess = owner.StartCoroutine(BlinkingProcess());

        yield return new WaitForSeconds(IframeC.iFrameTime);
        
        owner.StopCoroutine(BlinkProcess);
        
        SetAlphaAll(1);
        
        _healthS.IsActive = true;

        InvincibleProcess = null;
    }

    public IEnumerator BlinkingProcess()
    {
        yield return new WaitForSeconds(0.5f);
        var blinkTimer = new WaitForSeconds(IframeC.blinkPerTime);
        while (true)
        {
            SetAlphaAll(0);
            
            yield return blinkTimer;
            
            SetAlphaAll(1);
            
            yield return blinkTimer;
        }
    }
    
    private void SetAlphaAll(float alpha)
    {
        foreach (var r in renderers.renderers)
        {
            Color alphaZero = r.color;
            alphaZero.a = alpha;

            r.color = alphaZero;
        }
    }
    public void Dispose()
    {
        _healthC.OnTakeHit -= OnTakeHit;
    }
}

[System.Serializable]
public class IFrameComponent : IComponent
{
    public float iFrameTime = 0.5f;
    public float blinkPerTime = 0.1f;
}
