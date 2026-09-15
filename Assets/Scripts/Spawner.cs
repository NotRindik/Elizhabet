using System;
using Sirenix.OdinInspector;
using UnityEngine;

public class Spawner : SerializedMonoBehaviour
{
    [SerializeField] SpawnEffect spawnEffect;

    public void Spawn(AbstractEntity prefab, Action<AbstractEntity> onSpawned)
    {
        if (spawnEffect != null) spawnEffect.Execute(this, () => SpawnInstance(prefab, onSpawned));
        else SpawnInstance(prefab, onSpawned);
    }

    void SpawnInstance(AbstractEntity prefab, Action<AbstractEntity> onSpawned)
    {
        Debug.Log("Spawn");
        var instance = Instantiate(prefab, transform.position, transform.rotation);
        onSpawned?.Invoke(instance);
    }
}

[Serializable]
public abstract class SpawnEffect
{
    public abstract void Execute(Spawner spawner, Action onComplete);
}

[Serializable]
public class MagicalCircleEffect : SpawnEffect
{
    public MagicCircle Circle;
    public float spawnAfter;
    public override void Execute(Spawner spawner, Action onComplete)
    {
        Circle.Restart();
        Circle.OnTickUnnormalized.AddListener(OnComplete);
        void OnComplete(float t) {
            if(spawnAfter > t)
                return;
            Circle.OnTickUnnormalized.RemoveListener(OnComplete);
            onComplete.Invoke();
        }
    }
}