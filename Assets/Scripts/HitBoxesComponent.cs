using System;
using Systems;
using UnityEngine;

[Serializable]
public class HitBoxesComponent : IComponent
{
    public HitBoxContext Context;

    public bool IsHitboxExist => Context != null;
}