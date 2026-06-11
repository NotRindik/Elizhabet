using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

public class FunikulerFallScene : MonoBehaviour
{
    public CableRenderer cableRenderer;
    public Funikuler funik;
    public Coroutine _c;
    public SpriteRenderer Dver;

    public AudioClip CloseSound,Grohot,Ehanie,fallSound;
    
    [Header("Fake Physics")]
    public Transform funikulerVisual;
    public float maxTilt = 15f;
    public float tiltMultiplier = 250f;
    public float tiltSmooth = 5f;
    
    private float _lastPressure;
    private float _currentTilt;
    private float _tiltVelocity;
    
    
    private void LateUpdate()
    {
        if (cableRenderer == null || funikulerVisual == null)
        {
            funikulerVisual = funik.transform;
        }

        float delta =
            cableRenderer.pressurePoint -
            _lastPressure;

        _lastPressure =
            cableRenderer.pressurePoint;

        float targetTilt =
            Mathf.Clamp(
                -delta * tiltMultiplier,
                -maxTilt,
                maxTilt
            );

        _currentTilt =
            Mathf.SmoothDamp(
                _currentTilt,
                targetTilt,
                ref _tiltVelocity,
                1f / tiltSmooth
            );

        funikulerVisual.localRotation =
            Quaternion.Euler(
                0,
                0,
                _currentTilt
            );
    }
    
    
    [ContextMenu("Start")]
    public void StartMove()
    {
        _c = StartCoroutine(Sequence());
    }

    public IEnumerator Sequence()
    {
        if(CloseSound)
            AudioManager.instance.PlaySoundEffect(CloseSound);

        Dver.transform.DOScaleX(10.5f,0.2f);
        Dver.transform.DOLocalMoveX(-0.2797f,0.2f);
        
        var grohot = AudioManager.instance.PlaySoundEffect(Grohot,volume:0.8f);
        
        PlayerCamShake.Instance.SetShake(new ShakeData(3,3));
        
        
        yield return new WaitForSeconds(1);
        
        yield return ShakePressure(0.001f, 12f, 3);
        
        PlayerCamShake.Instance.SetShake(default);
        
        AudioManager.instance.StopSoundEffect(grohot);
        
        
        
        yield return new WaitForSeconds(0.3f);
        
        yield return MovePressure(0.5f, 10,AnimationCurve.EaseInOut(0,0,1,1));
        yield return new WaitForSeconds(0.1f);
        
        PlayerCamShake.Instance.SetShake(new ShakeData(3,3));
        
        grohot = AudioManager.instance.PlaySoundEffect(Grohot,volume:0.8f);
        
        StartCoroutine(ShakePressure(0.001f,6,4f));
        StartCoroutine(AnimateWeightForce(2, 4.4f));
        yield return new WaitForSeconds(4.4f);
        
        PlayerCamShake.Instance.SetShake(default);
        AudioManager.instance.StopSoundEffect(grohot);
        AudioManager.instance.PlaySoundEffect(fallSound,volume:0.5f);
        cableRenderer.BreakCable();
        funik.CableRenderer = null;
        
        funik.rb.bodyType = RigidbodyType2D.Dynamic;
        
        funik.rb.AddTorque(30);
        funik.rb.AddForceY(10,ForceMode2D.Impulse);
    }

    public IEnumerator MovePressure(float target, float duration, AnimationCurve curve = null)
    {
        float time = 0f;
        float start = cableRenderer.pressurePoint;

        
        const float soundInterval = 0.1f;
        float soundTimer = 0f;
        
        curve ??= AnimationCurve.Linear(0, 0, 1, 1);

        while (time < duration)
        {
            float delta = Time.deltaTime;

            time += delta;
            soundTimer += delta;
            
            if (soundTimer >= soundInterval)
            {
                soundTimer = 0f;

                AudioManager.instance.PlaySoundEffect(Ehanie);
            }
            
            float t = Mathf.Clamp01(time / duration);
            t = curve.Evaluate(t);

            cableRenderer.pressurePoint =
                Mathf.Lerp(start, target, t);

            UpdateWeightForce();

            yield return null;
        }

        cableRenderer.pressurePoint = target;
        UpdateWeightForce();
    }

    public IEnumerator ShakePressure(float amplitude, float speed, float duration, float centerOffset = 0f)
    {
        float time = 0f;
        float start =
            cableRenderer.pressurePoint +
            centerOffset;

        while (time < duration)
        {
            time += Time.deltaTime;

            float offset =
                Mathf.Sin(time * speed) *
                amplitude;

            cableRenderer.pressurePoint =
                start + offset;

            UpdateWeightForce();

            yield return null;
        }

        cableRenderer.pressurePoint = start;
        UpdateWeightForce();
    }
    public IEnumerator ShakePressureForce(float amplitude, float speed, float duration, float center = 0f)
    {
        float time = 0f;
        float baseValue = cableRenderer.weightForce + center;

        while (time < duration)
        {
            time += Time.deltaTime;

            float offset = Mathf.Sin(time * speed) * amplitude;

            cableRenderer.weightForce = baseValue + offset;

            yield return null;
        }

        cableRenderer.weightForce = baseValue;
    }
    private void UpdateWeightForce()
    {
        float t = Mathf.InverseLerp(
            0.066f,
            0.5f,
            cableRenderer.pressurePoint);

        cableRenderer.weightForce =
            Mathf.Lerp(1.41f, 4f, t);
    }
    
    public IEnumerator AnimateWeightForce(float delta, float duration, AnimationCurve curve = null)
    {
        float time = 0f;
        float start = cableRenderer.weightForce;
        float target = start + delta;

        curve ??= AnimationCurve.Linear(0, 0, 1, 1);

        while (time < duration)
        {
            time += Time.deltaTime;

            float t = Mathf.Clamp01(time / duration);
            t = curve.Evaluate(t);

            cableRenderer.weightForce =
                Mathf.Lerp(start, target, t);

            yield return null;
        }

        cableRenderer.weightForce = target;
    }
}
