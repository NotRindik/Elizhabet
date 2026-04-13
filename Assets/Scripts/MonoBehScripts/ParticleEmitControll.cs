using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class ParticleEmitController : MonoBehaviour
{
    [SerializeField] private ParticleSystem particleSystem;

    public float rate;
    public int count;
    public int framesToInvokeAfterEmit = 1;

    public BetterEvent onEmit;
    public BetterEvent onAfterEmit;

    private float timer;
    
    private int afterEmitQueue;

    private void Awake()
    {
        particleSystem ??= GetComponent<ParticleSystem>();
    }

    void Update()
    {
        if (afterEmitQueue >= framesToInvokeAfterEmit)
        {
            int invokeCount = afterEmitQueue;
            afterEmitQueue = 0;

            for (int i = 0; i < invokeCount; i++)
                onAfterEmit.Invoke();
        }
        
        timer += Time.deltaTime * rate;

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
        
        afterEmitQueue++;
    }
}