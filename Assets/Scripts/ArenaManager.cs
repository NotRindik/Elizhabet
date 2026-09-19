using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using Systems;
using UnityEngine;

public class ArenaManager : SerializedMonoBehaviour
{
    public Wave[] waves;
    public Spawner[] spawners;

    [MinMaxSlider(0.01f, 1f, true)]
    public Vector2 spawnDelayRange = new(0.02f, 0.3f);

    public BetterEvent OnArenaStart;
    public BetterEvent OnArenaEnd;
    public BetterEvent OnNextWave;

    int currentWaveIndex;
    float waveTimer;
    readonly List<AbstractEntity> aliveEntities = new();
    bool waveActive;
    bool isSpawning;
    CancellationTokenSource cts;

    [Serializable]
    public class Wave
    {
        public SpawnData[] spawnData;
        public float time;

        [Serializable]
        public class SpawnData
        {
            public AbstractEntity prefab;
            public int spawnerIndex;
        }
    }


    public void StartArenaWithDelay(float delay)
    {
        var _ = StartArena(delay);
    }
    
    private async UniTaskVoid StartArena(float delay)
    {
        await UniTask.WaitForSeconds(delay);

        foreach (var spw in spawners)
        {
            spw.ResetEffects();
        }

        StartArena();
    }
    

    public void StartArena()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

        OnArenaStart.Invoke();
        StartWave(0).Forget();
    }

    async UniTaskVoid StartWave(int index)
    {
        var ct = cts.Token;

        currentWaveIndex = index;
        aliveEntities.Clear();
        waveActive = false;
        isSpawning = true;

        var wave = waves[index];
        
        await UniTask.WaitUntil(() => AreSpawnersReady(wave), cancellationToken: ct);
        
        var running = new List<UniTask>(wave.spawnData.Length);
        for (int i = 0; i < wave.spawnData.Length; i++)
        {
            var data = wave.spawnData[i];
            var spawner = spawners[data.spawnerIndex];

            running.Add(spawner.SpawnAsync(data.prefab, OnEntitySpawned, ct));

            if (i < wave.spawnData.Length - 1)
                await UniTask.WaitForSeconds(UnityEngine.Random.Range(spawnDelayRange.x, spawnDelayRange.y),
                                             cancellationToken: ct);
        }
        await UniTask.WhenAll(running);

        isSpawning = false;
        waveTimer = wave.time;
        waveActive = true;
        
        if (wave.spawnData.Length > 0 && aliveEntities.Count == 0)
            CompleteWave();
    }

    bool AreSpawnersReady(Wave wave)
    {
        for (int i = 0; i < wave.spawnData.Length; i++)
            if (!spawners[wave.spawnData[i].spawnerIndex].IsReadyToSpawn)
                return false;
        return true;
    }

    void OnEntitySpawned(AbstractEntity entity)
    {
        aliveEntities.Add(entity);
        entity.GetControllerComponent<HealthComponent>().OnDie += OnEntityDeath;

        entity.GetControllerSystem<VisionMemorySystem>().IsActive = false;
        entity.GetControllerComponent<VisionComponent>().currentTarget = ContextManager.Instance.player.transform;
    }

    void OnEntityDeath(AbstractEntity entity)
    {
        aliveEntities.Remove(entity);
        entity.GetControllerComponent<HealthComponent>().OnDie -= OnEntityDeath;

        if (isSpawning) return;
        if (aliveEntities.Count == 0) CompleteWave();
    }

    void Update()
    {
        if (!waveActive) return;
        if (waves[currentWaveIndex].time <= 0) return;

        waveTimer -= Time.deltaTime;
        if (waveTimer <= 0) CompleteWave();
    }

    void CompleteWave()
    {
        if (!waveActive && !isSpawning) return;
        waveActive = false;

        if (currentWaveIndex + 1 < waves.Length)
        {
            OnNextWave.Invoke();
            StartWave(currentWaveIndex + 1).Forget();
        }
        else
        {
            TimeManager.LockedHitStop(1,0.2f,1f);
            OnArenaEnd.Invoke();
        }
    }

    void OnDestroy()
    {
        cts?.Cancel();
        cts?.Dispose();
    }
}