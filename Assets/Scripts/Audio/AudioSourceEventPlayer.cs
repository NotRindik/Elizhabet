using System;
using UnityEngine;

public class AudioSourceEventPlayer : MonoBehaviour
{
    public AudioSource audioSource;
    public EventSound audioEvent;

    private void Awake()
    {
        audioSource ??= GetComponent<AudioSource>();
    }

    public void Play()
    {
        audioSource.clip = new EventSoundInstance(audioEvent).Init();
        audioSource.Play();
    }

}
