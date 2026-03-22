using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(menuName = "Audio/Material")]
public class ObjectAudioMaterial : ScriptableObject
{
    public AudioClip[] clips;
}