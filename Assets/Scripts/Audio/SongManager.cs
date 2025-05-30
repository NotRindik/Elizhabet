using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class SongManager : MonoBehaviour
{
    public AudioClip[] audioClips;
    private void Start()
    {
        int i = Random.Range(0,audioClips.Length);
        
        AudioManager.instance.PlayTrack(audioClips[i],volumeCap:2);
    }
}
