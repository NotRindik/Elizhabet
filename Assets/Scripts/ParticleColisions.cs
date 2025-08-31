using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class ParticleColisions : MonoBehaviour
{
    public UnityEvent OnParticleColide;

    public UnityEvent OnParticleCollideLessUpdate;

    public Coroutine process;

    private void OnParticleCollision(GameObject other)
    {
        OnParticleColide?.Invoke();
        if(process == null) 
            process = StartCoroutine(OnColideLess());
    }

    public IEnumerator OnColideLess()
    {
        OnParticleCollideLessUpdate?.Invoke();
        yield return new WaitForSeconds(0.2f);
        process = null;
    }
}