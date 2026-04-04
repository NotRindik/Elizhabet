using UnityEngine;
using UnityEngine.Serialization;

public class AudioMaterialSetter : MonoBehaviour
{
    [SerializeField] private ObjectAudioMaterial audioMaterial;
    
    public ObjectAudioMaterial AudioMaterial
    {
        get { return audioMaterial; }
        set { audioMaterial = value; }
    }
}
