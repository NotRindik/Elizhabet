using Cinemachine;
using System.Collections;
using UnityEngine;

public class PlayerCamShake : MonoBehaviour
{
    private PlayerCamShake _Inst;
    public ShakeData shakeData = new ShakeData(1,0);

    public PlayerCamShake Instance { get => _Inst; set => _Inst = value; }
    private CinemachineBasicMultiChannelPerlin _perlin;

    private Coroutine _shakeProcess;
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(this.gameObject);
        }
        _perlin = GetComponent<CinemachineVirtualCamera>().GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
    }

    public void SetShake(ShakeData data)
    {
        if (_perlin == null)
            return;

        _perlin.m_AmplitudeGain = data.amplitude;
        _perlin.m_FrequencyGain = data.frequency;
    }

    public void Shake(ShakeData data,float time,float delay = 0)
    {
        if (_perlin == null)
            return;
        if (_shakeProcess != null)
            StopCoroutine(_shakeProcess);
        _shakeProcess = StartCoroutine(ShakeProcess(data,time,delay));
    }

    private IEnumerator ShakeProcess(ShakeData data, float time, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        float t = 0f;

        _perlin.m_FrequencyGain = data.frequency;

        float damping = 6f / time;

        while (t < time)
        {
            float k = Mathf.Exp(-t * damping);
            _perlin.m_AmplitudeGain = data.amplitude * k;

            t += Time.deltaTime;
            yield return null;
        }

        _perlin.m_AmplitudeGain = 0f;
        _shakeProcess = null;
    }
}

public struct ShakeData
{
    public float frequency,amplitude;

    public ShakeData(float frequency,float amplitude)
    {
        this.frequency = frequency;
        this.amplitude = amplitude;
    }
}