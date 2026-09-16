using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

public class Spawner : SerializedMonoBehaviour
{
    [SerializeField] SpawnEffect spawnEffect;

    bool isBusy;
    
    public bool IsReadyToSpawn => !isBusy && (spawnEffect == null || spawnEffect.IsEffectCompleted);
    
    public async UniTask SpawnAsync(AbstractEntity prefab, Action<AbstractEntity> onSpawned, CancellationToken ct = default)
    {
        await UniTask.WaitUntil(() => IsReadyToSpawn, cancellationToken: ct);

        isBusy = true;
        try
        {
            if (spawnEffect == null)
            {
                SpawnInstance(prefab, onSpawned);
                return;
            }

            var spawnMoment = new UniTaskCompletionSource();
            var effectDone = new UniTaskCompletionSource();

            spawnEffect.Execute(this,
                onSpawnMoment: () => spawnMoment.TrySetResult(),
                onEffectCompleted: () => effectDone.TrySetResult());

            await spawnMoment.Task.AttachExternalCancellation(ct);
            SpawnInstance(prefab, onSpawned);

            await effectDone.Task.AttachExternalCancellation(ct);
        }
        finally
        {
            isBusy = false;
        }
    }

    void SpawnInstance(AbstractEntity prefab, Action<AbstractEntity> onSpawned)
    {
        var instance = Instantiate(prefab, transform.position, transform.rotation);
        onSpawned?.Invoke(instance);
    }
}

[Serializable]
public abstract class SpawnEffect
{
    public bool IsEffectCompleted = true;

    public abstract void Execute(Spawner spawner, Action onSpawnMoment, Action onEffectCompleted);
}

[Serializable]
public class MagicalCircleEffect : SpawnEffect
{
    public MagicCircle Circle;
    public float spawnAfter;

    public override void Execute(Spawner spawner, Action onSpawnMoment, Action onEffectCompleted)
    {
        IsEffectCompleted = false;
        bool spawned = false;

        Circle.Restart();
        Circle.OnTickUnnormalized.AddListener(OnTick);

        void OnTick(float t)
        {
            if (!spawned && t >= spawnAfter)
            {
                spawned = true;
                onSpawnMoment?.Invoke();
            }

            if (t < 1f) return;

            Circle.OnTickUnnormalized.RemoveListener(OnTick);

            if (!spawned)
            {
                spawned = true;
                onSpawnMoment?.Invoke();
            }

            IsEffectCompleted = true;
            onEffectCompleted?.Invoke();
        }
    }
}