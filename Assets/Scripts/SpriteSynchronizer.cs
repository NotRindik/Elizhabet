using AYellowpaper.SerializedCollections;
using System;
using Systems;
using UnityEngine;

[Serializable]
public class RendererCollection : IComponent
{
    public SerializedDictionary<string, SpriteRenderer> renderers;
}
