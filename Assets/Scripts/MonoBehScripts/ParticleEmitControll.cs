using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(ParticleSystem))]
public class ParticleEmitController : MonoBehaviour
{
    [SerializeField] private ParticleSystem particleSystem;

    public float rate;
    public int count;
    public float secToInvokeAfterEmit = 1f;

    public BetterEvent onEmit;
    public BetterEvent onAfterEmit;

    private float timer;

    private readonly List<float> afterEmitTimers = new();

    private void Awake()
    {
        particleSystem ??= GetComponent<ParticleSystem>();
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // Обработка таймеров afterEmit
        for (int i = afterEmitTimers.Count - 1; i >= 0; i--)
        {
            afterEmitTimers[i] -= dt;

            if (afterEmitTimers[i] <= 0f)
            {
                onAfterEmit.Invoke();
                afterEmitTimers.RemoveAt(i);
            }
        }

        timer += dt * rate;

        while (timer >= 1f)
        {
            timer -= 1f;
            Emit(count);
        }
    }

    public void Emit(int count = 1)
    {
        particleSystem.Emit(count);
        onEmit.Invoke();

        // Добавляем таймер
        afterEmitTimers.Add(secToInvokeAfterEmit);
    }
}