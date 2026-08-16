using System.Collections;
using Sirenix.OdinInspector;
using Systems;
using UnityEngine;
using Random = UnityEngine.Random;

public class EyeController : SerializedMonoBehaviour
{
    public Animator leftEyeAnim, rightEyeAnim;
    public AbstractEntity entity => ContextManager.Instance.player;
    public HealthComponent HealthComponent => entity.GetControllerComponent<HealthComponent>();

    private Coroutine eyeSequence;

    [MinMaxSlider(0,20)]
    public Vector2 blinkRandomSecRange;

    private void Start()
    {
        HealthComponent.OnTakeHit += OnTakeDamage;
        StartBlinking();
    }

    public void StartBlinking()
    {
        eyeSequence = StartCoroutine(BlinkProcess());
    }

    public IEnumerator BlinkProcess()
    {
        while (true)
        {
            int oneOrDouble = Random.Range(0, 2);

            if (oneOrDouble == 0)
            {
                yield return OneBlink();
            }
            else
            {
                yield return DoubleBlink();
            }
        }
    }

    public IEnumerator OneBlink()
    {
        leftEyeAnim.Play("Idle");
        rightEyeAnim.Play("Idle");
        yield return new WaitForSeconds(Random.Range(blinkRandomSecRange.x, blinkRandomSecRange.y));
        leftEyeAnim.Play("Blink");
        rightEyeAnim.Play("Blink");
        yield return new WaitForSeconds(0.8f);
    }
    
    public IEnumerator DoubleBlink()
    {
        leftEyeAnim.Play("Idle");
        rightEyeAnim.Play("Idle");
        yield return new WaitForSeconds(Random.Range(blinkRandomSecRange.x, blinkRandomSecRange.y));
        leftEyeAnim.Play("Blink");
        rightEyeAnim.Play("Blink");
        yield return new WaitForSeconds(0.8f);
        leftEyeAnim.Play("Idle");
        rightEyeAnim.Play("Idle");
        yield return new WaitForSeconds(0.2f);
        leftEyeAnim.Play("Blink");
        rightEyeAnim.Play("Blink");
    }
    
    
    public IEnumerator TakeDamageProcess()
    {
        leftEyeAnim.Play("Closed");
        rightEyeAnim.Play("Closed");

        yield return new WaitForSeconds(1);
        
        StartBlinking();
    }


    public void OnTakeDamage(HitInfo hitInfo)
    {
        if (eyeSequence != null)
        {
            StopCoroutine(eyeSequence);
        }
        
        eyeSequence = StartCoroutine(TakeDamageProcess());
    }

    private void OnDestroy()
    {
        if (eyeSequence != null)
        {
            StopCoroutine(eyeSequence);
        }
        
        if(entity)HealthComponent.OnTakeHit -= OnTakeDamage;
    }
}
