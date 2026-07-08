// Editor/AnimatorRegistry.cs
// Отдельный файл — чтобы AnimationStateConfig мог его видеть через #if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

public static class AnimatorRegistry
{
    public static readonly Dictionary<string, Animator> Animators = new();
}
