using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Systems;
using UnityEngine;

public class ArenaManager : SerializedMonoBehaviour
{
    public Wave[] waves;
    public Spawner[] spawners;

    public BetterEvent OnArenaStart;
    public BetterEvent OnArenaEnd;
    public BetterEvent OnNextWave;

    int currentWaveIndex;
    float waveTimer;
    readonly List<AbstractEntity> aliveEntities = new();
    bool waveActive;

    [System.Serializable]
    public class Wave
    {
        public SpawnData[] spawnData;
        public float time;
        
        [System.Serializable]
        public class SpawnData
        {
            public AbstractEntity prefab;
            public int spawnerIndex;
        }
    }

    public void StartArena()
    {
        OnArenaStart.Invoke();
        StartWave(0);
    }

    void StartWave(int index)
    {
        currentWaveIndex = index;
        waveTimer = waves[index].time;
        aliveEntities.Clear();
        waveActive = true;

        var wave = waves[index];
        for (int i = 0; i < wave.spawnData.Length; i++)
        {
            var data = wave.spawnData[i];
            var spawner = spawners[data.spawnerIndex];
            spawner.Spawn(data.prefab, OnEntitySpawned);
        }
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
        waveActive = false;
        if (currentWaveIndex + 1 < waves.Length)
        {
            OnNextWave.Invoke();
            StartWave(currentWaveIndex + 1);
        }
        else OnArenaEnd.Invoke();
    }
}