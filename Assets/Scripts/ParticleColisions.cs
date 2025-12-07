using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public unsafe class ParticleColisions : MonoBehaviour
{
    public UnityEvent OnParticleColide;

    public UnityEvent OnParticleCollideLessUpdate;

    public ParticleSystem ps;

    private ParticleSystem.Particle[] particles;

    public Coroutine process;
    public float minVelocity = 3f;
    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        particles = new ParticleSystem.Particle[1024];
    }

    private void Update()
    {
        int count = ps.GetParticles(particles);
        fixed (ParticleSystem.Particle* psPtr = &particles[0])
        {
            for (int i = 0; i < count; i++)
            {
                var p = psPtr[i];

                Vector3 vel = p.velocity;

                if (vel.magnitude >= minVelocity)
                {
                    OnParticleColide?.Invoke();
                    if (process == null)
                        process = StartCoroutine(OnColideLess());
                }
            }
        }
    }


/*    private void OnParticleCollision(GameObject other)
    {
        OnParticleColide?.Invoke();
        if(process == null) 
            process = StartCoroutine(OnColideLess());
    }*/

    public IEnumerator OnColideLess()
    {
        OnParticleCollideLessUpdate?.Invoke();
        yield return new WaitForSeconds(0.2f);
        process = null;
    }
}