using UnityEngine;


public class SoundEmiter : MonoBehaviour
{
    [Header("Клипы")]
    public EventSoundInstance sound;

    public void Play()
    {
        AudioManager.instance.PlayEvent(sound);
    }
}