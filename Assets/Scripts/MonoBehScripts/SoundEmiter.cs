using UnityEngine;


public class SoundEmiter : MonoBehaviour
{
    [Header("Клипы")]
    public AudioClip[] clips;

    [Header("Громкость")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Header("Рандомизация питча (min,max)")]
    public Vector2 randomisePitch = new Vector2(1f, 1f);

    public void Play()
    {
        if (clips == null || clips.Length == 0)
        {
            Debug.LogWarning("SoundEmiter: Нет клипов для проигрывания!");
            return;
        }

        // выбираем случайный клип
        AudioClip chosenClip;
        if (clips.Length == 1)
            chosenClip = clips[0];
        else
            chosenClip = clips[Random.Range(0, clips.Length)];

        // выбираем случайный питч
        float pitch = Random.Range(randomisePitch.x, randomisePitch.y);

        // проигрываем через AudioManager
        AudioManager.instance.PlaySoundEffect(clip: chosenClip, volume: volume, pitch: pitch);
    }
}