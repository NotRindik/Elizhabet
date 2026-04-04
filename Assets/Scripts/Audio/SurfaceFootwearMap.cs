using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class SurfaceFootwearMap : SerializedScriptableObject
{
    public Dictionary<ObjectAudioMaterial, FootwearEntry> map;
}

[System.Serializable]
public class FootwearEntry
{
    public Dictionary<ObjectAudioMaterial, AudioClip[]> combinations;
}