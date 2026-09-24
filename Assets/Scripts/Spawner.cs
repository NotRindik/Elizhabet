using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Spawner : SerializedMonoBehaviour
{
    [SerializeField] SpawnEffect spawnEffect;

    bool isBusy;

    public void ResetEffects()
    {
        Debug.Log("Effect Reseted");
        spawnEffect.IsEffectCompleted = true;
        isBusy = false;
    }
    
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
        var instance = GameScene.Instantiate(prefab, transform.position, transform.rotation);
        onSpawned?.Invoke(instance);
    }
}


public static class GameScene
{
    public static T Instantiate<T>(T prefab, Vector3 pos, Quaternion rot)
        where T : UnityEngine.Object
    {
        var inst = UnityEngine.Object.Instantiate(prefab, pos, rot);

        if (inst is GameObject go)
        {
            SceneManager.MoveGameObjectToScene(
                go,
                SceneLoader.SceneFlow.CurrentScene);
        }
        else if (inst is Component component)
        {
            SceneManager.MoveGameObjectToScene(
                component.gameObject,
                SceneLoader.SceneFlow.CurrentScene);
        }
        else
        {
            Debug.LogWarning(
                "You try Instantiate not GameObject nor Component. " +
                "If it's just Object, use Object.Instantiate instead.");
        }

        return inst;
    }
}

[Serializable]
public abstract class SpawnEffect
{
    [NonSerialized] public bool IsEffectCompleted = true;

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