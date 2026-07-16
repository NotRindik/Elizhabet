using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using Systems;
using UnityEngine;
using Object = UnityEngine.Object;

public class SelfDestruction : SerializedMonoBehaviour
{
    public DestructionType[] DestructPipeLine = {new Destroy()};
    [SerializeField] private DestructionData _destructionData = new DestructionData()
    {
        timeBefDestroy = 10
    };
    public ref DestructionData DestructionData => ref _destructionData;

    private AbstractEntity _abstractEntity;
    private HealthComponent _healthComponent;
    public void Start()
    {
        _abstractEntity = GetComponent<AbstractEntity>();
        _healthComponent = _abstractEntity.GetControllerComponent<HealthComponent>();

        _healthComponent.OnDie += PerformDestruct;
    }

    public void PerformDestruct(AbstractEntity _) => Destruct();

    public void Destruct()
    {
        foreach (var data in DestructPipeLine)
        {
            data.Destruct(this);   
        }
    }

    private void OnDestroy()
    {
        _healthComponent.OnDie -= PerformDestruct;
    }
}


[Serializable]
public struct DestructionData
{
    public float timeBefDestroy;
}

public interface DestructionType
{
    public void Destruct(SelfDestruction slf);
}

public interface DestructLogic
{
    public void OnPerform(SelfDestruction slf);
}

public class SpriteDisappear : DestructionType
{
    public SpriteRenderer renderer;
    public float delay;
    public void Destruct(SelfDestruction slf)
    {
        renderer ??= slf.GetComponent<SpriteRenderer>();
        renderer
            .DOColor(new Color(0, 0, 0, 0), slf.DestructionData.timeBefDestroy-delay)
            .SetDelay(delay);
    }
}

public class ParticleDestruct : DestructionType
{
    public ParticleSystem ParticlePrefab;
    public void Destruct(SelfDestruction slf)
    {
        Object.Instantiate(ParticlePrefab,slf.transform.position,Quaternion.identity);
    }
}

public class SpriteArrayDisappear : DestructionType
{
    public SpriteRenderer[] renderers;
    public float delay;
    public void Destruct(SelfDestruction slf)
    {
        foreach (var renderer in renderers)
        {
            renderer
                .DOColor(new Color(0, 0, 0, 0), slf.DestructionData.timeBefDestroy-delay)
                .SetDelay(delay);   
        }
    }
}

public class AdditionMakeLogic : DestructionType
{
    public DestructLogic DestructLogic;

    public void Destruct(SelfDestruction slf)
    {
        DestructLogic.OnPerform(slf);
    }
}


public class Destroy : DestructionType
{

    public void Destruct(SelfDestruction slf)
    {
        Object.Destroy(slf.gameObject,slf.DestructionData.timeBefDestroy);
    }
}


public class PlayerDeathLogic : DestructLogic
{
    public float BlendDuration = 0.5f, Delay = 1f;
    public string BlendEffectName = "diagonal";
    public void OnPerform(SelfDestruction slf)
    {
        App.Instance.StartCoroutine(std.Utilities.Invoke(() => 
            TransitionEffect.Instance.BlendIn(BlendDuration, BlendEffectName), Delay));
    }
}