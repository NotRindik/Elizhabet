using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(menuName = "Audio/Material")]
public class ObjectAudioMaterial : SerializedScriptableObject
{
    [SerializeField] private Dictionary<string,AudioClip[]> clips = new ();

    public AudioClip[] GetSequence(string matInteraction)
    {
        return clips[matInteraction];
    }
}